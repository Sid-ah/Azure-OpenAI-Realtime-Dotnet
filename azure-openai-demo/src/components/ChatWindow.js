import React, { useEffect, useRef } from 'react';

function ChatWindow({ messages, currentTranscript }) {
  const chatContainerRef = useRef(null);

  useEffect(() => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTop = chatContainerRef.current.scrollHeight;
    }
  }, [messages, currentTranscript]);

  return (
    <div className="chat-container" ref={chatContainerRef}>
      {/* Display all previous messages */}
      {messages.map((msg, index) => (
        <div key={`msg-${index}`} className={`message ${msg.sender}`}>
          <div className="transcript">{msg.text}</div>
        </div>
      ))}
      
      {/* Display current transcript as a real-time typing indicator */}
      {currentTranscript && currentTranscript.trim() !== '' && (
        <div key="current-transcript" className="message assistant">
          <div className="transcript">{currentTranscript}</div>
        </div>
      )}
    </div>
  );
}

export default ChatWindow;