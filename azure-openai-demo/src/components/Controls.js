// src/components/Controls.js
import React, { useState, useRef, useEffect } from 'react';
import { createSession, connectRTC } from '../services/ApiService';

function Controls({ 
  isConnected, 
  setIsConnected, 
  updateStatus, 
  addLog, 
  settings, 
  addMessage, 
  updateAssistantMessage, 
  setCurrentTranscript,
  currentTranscript,
  status
}) {
  const [isRecording, setIsRecording] = useState(false);
  const [currentMessage, setCurrentMessage] = useState('');
  
  const peerConnectionRef = useRef(null);
  const dataChannelRef = useRef(null);
  const audioStreamRef = useRef(null);
  const mediaRecorderRef = useRef(null);
  const sessionIdRef = useRef(null);
  const ephKeyRef = useRef(null);

  // For audio processing
  const audioContextRef = useRef(null);
  const audioBufferRef = useRef(null);
  
  // Initialize audio context safely
  useEffect(() => {
    // Lazy initialize AudioContext only when needed
    if (!audioContextRef.current && typeof window !== 'undefined') {
      const AudioContext = window.AudioContext || window.webkitAudioContext;
      if (AudioContext) {
        audioContextRef.current = new AudioContext({ sampleRate: 24000 });
      }
    }
    
    return () => {
      // Cleanup when component unmounts
      if (audioContextRef.current) {
        audioContextRef.current.close();
      }
    };
  }, []);

  const startConversation = async () => {
    try {
      updateStatus('Initializing…');
      
      // Create session
      const sessionResponse = await createSession(settings.voice);
      sessionIdRef.current = sessionResponse.id;
      ephKeyRef.current = sessionResponse.client_secret.value;
      
      addLog(`Session ID → ${sessionIdRef.current}`);
      
      // Initialize WebRTC
      await initializeWebRTC();
      
      setIsConnected(true);
    } catch (err) {
      addLog(`❌ ${err.message}`);
      updateStatus('Failed');
    }
  };

  const stopConversation = () => {
    stopRecording();
    
    if (dataChannelRef.current) dataChannelRef.current.close();
    if (peerConnectionRef.current) peerConnectionRef.current.close();
    if (audioStreamRef.current) audioStreamRef.current.getTracks().forEach(t => t.stop());
    
    peerConnectionRef.current = null;
    dataChannelRef.current = null;
    audioStreamRef.current = null;
    mediaRecorderRef.current = null;
    
    setIsRecording(false);
    setIsConnected(false);
    updateStatus('Disconnected');
  };

  const initializeWebRTC = async () => {
    peerConnectionRef.current = new RTCPeerConnection({
      iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    // Remote audio playback
    peerConnectionRef.current.addEventListener('track', ({ track }) => {
      if (track.kind !== 'audio') return;
      const audio = new Audio();
      audio.srcObject = new MediaStream([track]);
      audio.play();
    });

    // DataChannel
    dataChannelRef.current = peerConnectionRef.current.createDataChannel('realtime');
    dataChannelRef.current.onopen = handleDataChannelOpen;
    dataChannelRef.current.onclose = () => addLog('DataChannel closed');
    dataChannelRef.current.onerror = (e) => addLog(`DataChannel error: ${e}`);
    dataChannelRef.current.onmessage = handleDataChannelMessage;

    // Local audio
    await setupAudio();

    const offer = await peerConnectionRef.current.createOffer({ offerToReceiveAudio: true });
    await peerConnectionRef.current.setLocalDescription(offer);
    await waitForIceGathering();

    const rtcUrl = `https://${settings.region}.realtimeapi-preview.ai.azure.com/v1/realtimertc?model=${settings.deploymentName}`;
    addLog(`RTC URL → ${rtcUrl}`);

    const answerSdp = await connectRTC(
      peerConnectionRef.current.localDescription.sdp,
      ephKeyRef.current,
      settings.deploymentName,
      settings.region,
      settings.ragEnabled ? {
        Enabled: settings.ragEnabled,
        SearchQuery: settings.ragQuery || "",
        TopK: settings.ragTopK || 3,
        RelevanceThreshold: 0.7
      } : null
    );

    await peerConnectionRef.current.setRemoteDescription({ type: 'answer', sdp: answerSdp });

    addLog('✅ WebRTC connected');
  };

  const setupAudio = async () => {
    // Get audio with specific constraints for 24kHz compatibility with Azure
    audioStreamRef.current = await navigator.mediaDevices.getUserMedia({ 
      audio: {
        channelCount: 1,       // Mono
        sampleRate: 24000,     // 24kHz as required by Azure
        echoCancellation: true,
        noiseSuppression: true,
      } 
    });

    // add track to peer connection early (before negotiating)
    audioStreamRef.current.getAudioTracks().forEach(track => 
      peerConnectionRef.current.addTrack(track, audioStreamRef.current)
    );

    // We'll still use webm/opus for recording as it's more efficient
    // but we'll convert to PCM before sending to Azure
    mediaRecorderRef.current = new MediaRecorder(audioStreamRef.current, { 
      mimeType: 'audio/webm;codecs=opus', 
      audioBitsPerSecond: 64000 
    });
  };

  const startRecording = () => {
    if (!mediaRecorderRef.current || isRecording) return;
    setIsRecording(true);
    mediaRecorderRef.current.start(100); // 100 ms chunks
    updateStatus('Recording');

    // Create an audio processor worklet for more reliable audio processing
    let audioProcessor = null;
    
    const setupAudioProcessor = async () => {
      if (!audioContextRef.current) {
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        audioContextRef.current = new AudioContext({ sampleRate: 24000 });
      }
      
      try {
        // Create a direct input source from the audio stream
        const source = audioContextRef.current.createMediaStreamSource(audioStreamRef.current);
        
        // Create an analyzer to get PCM data directly
        const analyzer = audioContextRef.current.createAnalyser();
        analyzer.fftSize = 2048;
        source.connect(analyzer);
        
        // Get PCM data directly from the analyzer
        const pcmProcessor = () => {
          const dataArray = new Float32Array(analyzer.fftSize);
          analyzer.getFloatTimeDomainData(dataArray);
          
          // Convert to 16-bit PCM
          const pcmData = new Int16Array(dataArray.length);
          
          // Convert Float32 to Int16
          for (let i = 0; i < dataArray.length; i++) {
            const s = Math.max(-1, Math.min(1, dataArray[i]));
            pcmData[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
          }
          
          // Convert to base64
          const base64 = arrayBufferToBase64(pcmData.buffer);
          
          // Send to server if connection is open
          if (dataChannelRef.current?.readyState === 'open' && isRecording) {
            dataChannelRef.current.send(JSON.stringify({
              type: 'input_audio_buffer.append',
              audio: base64
            }));
          }
        };
        
        // Process audio at regular intervals
        audioProcessor = setInterval(pcmProcessor, 100);
        return audioProcessor;
      } catch (error) {
        addLog(`❌ Audio processor setup error: ${error.message}`);
        return null;
      }
    };
    
    // Set up the audio processor
    setupAudioProcessor().then(processor => {
      audioBufferRef.current = processor;
    });
    
    // For compatibility, still handle the MediaRecorder data
    mediaRecorderRef.current.ondataavailable = async (evt) => {
      if (evt.data.size === 0) return;
      // We're not using this data anymore as we're processing audio directly
      // But we'll keep the event handler for debugging purposes
      addLog(`Audio chunk size: ${evt.data.size}`);
    };
  };
  
  // Helper function to convert AudioBuffer to 16-bit PCM
  const convertToPCM16 = (audioBuffer) => {
    const channelData = audioBuffer.getChannelData(0); // Mono - get the first channel
    const pcmData = new Int16Array(channelData.length);
    
    // Convert Float32 to Int16
    for (let i = 0; i < channelData.length; i++) {
      // Convert float (-1.0 to 1.0) to int16 (-32768 to 32767)
      const s = Math.max(-1, Math.min(1, channelData[i]));
      pcmData[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
    }
    
    return pcmData;
  };
  
  // Helper function to convert ArrayBuffer to base64
  const arrayBufferToBase64 = (buffer) => {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  };

  const stopRecording = () => {
    if (!isRecording) return;
    setIsRecording(false);
    mediaRecorderRef.current.stop();
    updateStatus('Stopped recording');
    
    // Clear the audio processor interval
    if (audioBufferRef.current) {
      clearInterval(audioBufferRef.current);
      audioBufferRef.current = null;
    }
    
    if (dataChannelRef.current?.readyState === 'open') {
      dataChannelRef.current.send(JSON.stringify({ type: 'input_audio_buffer.clear' }));
    }
  };

  const waitForIceGathering = () => {
    return new Promise((resolve) => {
      if (peerConnectionRef.current.iceGatheringState === 'complete') return resolve();
      const handler = () => {
        if (peerConnectionRef.current.iceGatheringState === 'complete') {
          peerConnectionRef.current.removeEventListener('icegatheringstatechange', handler);
          resolve();
        }
      };
      peerConnectionRef.current.addEventListener('icegatheringstatechange', handler);
      setTimeout(resolve, 7000); // failsafe
    });
  };

  const handleDataChannelOpen = () => {
    addLog('DataChannel open – sending session.update');
    updateStatus('Connected');

    // Base configuration for session update
    const baseInstructions = 'You are a helpful AI assistant. Respond courteously and concisely.';
    
    // Enhanced instructions if RAG is enabled
    let instructions = baseInstructions;
    if (settings.ragEnabled && settings.ragQuery) {
      instructions = `${baseInstructions} Use the context information from relevant documents when available to provide accurate answers.`;
    }
    
    const cfg = {
      type: 'session.update',
      session: {
        instructions: instructions,
        modalities: ['audio', 'text'],
        input_audio_transcription: {
          model: 'whisper-1'
        },
        turn_detection: {
          type: 'server_vad',
          threshold: 0.5,
          prefix_padding_ms: 300,
          silence_duration_ms: 350,
          create_response: true
        }
      }
    };
    dataChannelRef.current.send(JSON.stringify(cfg));
    startRecording();
  };

  const handleDataChannelMessage = ({ data }) => {
    let msg;
    try { msg = JSON.parse(data); } catch { return; }
    addLog(`⬅ ${msg.type}`);

    switch (msg.type) {
      case 'session.created':
        break;

      case 'conversation.item.input_audio_transcription.completed':
        // Add user message from speech transcription
        addMessage('user', msg.transcript ?? '');
        break;

      case 'response.created':
        // Reset transcript when a response starts
        setCurrentTranscript('');
        break;

      case 'response.text_delta':
      case 'response.delta': // newer schema
        // Accumulate delta updates to the current transcript
        if (msg.delta?.text) {
          updateAssistantMessage(msg.delta.text);
        } else if (msg.delta?.content) {
          updateAssistantMessage(msg.delta.content);
        }
        break;

      case 'response.output_item.done':
        if (msg.item?.content[0]?.transcript) {
          // Each completed item is a full assistant response bubble
          const transcript = msg.item.content[0].transcript;
          addLog(`Assistant response received: ${transcript.substring(0, 20)}...`);
          addMessage('assistant', transcript);
          // Clear current transcript placeholder after adding to history
          setCurrentTranscript('');
        }
        break;

      case 'response.completed':
        // Response fully completed; nothing to accumulate as bubbles already added
        // Ensure placeholder is cleared
        setCurrentTranscript('');
        break;

      case 'error':
        addLog(`❌ ${msg.error?.message || 'Unknown error'}`);
        updateStatus(`Error: ${msg.error?.message || ''}`);
        break;

      default:
        // other event types ignored
    }
  };

  return (
    <div className="controls">
      <button 
        onClick={startConversation} 
        disabled={isConnected}
      >
        {isRecording && <span className="recording-indicator"></span>}
        Start Conversation
      </button>
      <button 
        onClick={stopConversation} 
        disabled={!isConnected}
      >
        End Conversation
      </button>
      <span className="status-indicator">{status}</span>
    </div>
  );
}

export default Controls;