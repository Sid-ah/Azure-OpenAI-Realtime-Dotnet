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
      {messages.map((msg, index) => (
        <div key={index} className={`message ${msg.sender}`}>
          <div className="transcript">{msg.text}</div>
        </div>
      ))}
      {currentTranscript && (
        <div className="message assistant">
          <div className="transcript">{currentTranscript}</div>
        </div>
      )}
    </div>
  );
}

export default ChatWindow;