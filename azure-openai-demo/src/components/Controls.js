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
      settings.region
    );

    await peerConnectionRef.current.setRemoteDescription({ type: 'answer', sdp: answerSdp });

    addLog('✅ WebRTC connected');
  };

  const setupAudio = async () => {
    audioStreamRef.current = await navigator.mediaDevices.getUserMedia({ audio: true });

    // add track to peer connection early (before negotiating)
    audioStreamRef.current.getAudioTracks().forEach(track => 
      peerConnectionRef.current.addTrack(track, audioStreamRef.current)
    );

    // recorder → webm/opus chunks (lower latency than PCM but works fine with Whisper)
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

    mediaRecorderRef.current.ondataavailable = (evt) => {
      if (evt.data.size === 0) return;
      const reader = new FileReader();
      reader.onloadend = () => {
        const base64 = reader.result.split(',')[1];
        if (dataChannelRef.current?.readyState === 'open') {
          dataChannelRef.current.send(JSON.stringify({
            type: 'input_audio_buffer.append',
            audio: base64
          }));
        }
      };
      reader.readAsDataURL(evt.data);
    };
  };

  const stopRecording = () => {
    if (!isRecording) return;
    setIsRecording(false);
    mediaRecorderRef.current.stop();
    updateStatus('Stopped recording');
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

    const cfg = {
      type: 'session.update',
      session: {
        instructions: 'You are a helpful AI assistant. Respond courteously and concisely.',
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
        addMessage('user', msg.transcript ?? '');
        break;

      case 'response.created':
        setCurrentTranscript('');
        break;

      case 'response.text_delta':
      case 'response.delta': // newer schema
        if (msg.delta?.text) {
          updateAssistantMessage(msg.delta.text);
        } else if (msg.delta?.content) {
          updateAssistantMessage(msg.delta.content);
        }
        break;

      case 'response.done':
      case 'response.completed':
        addMessage('assistant', currentTranscript);
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