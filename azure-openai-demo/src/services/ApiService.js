// src/services/ApiService.js
const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5126/api/AzureOpenAI';

export const createSession = async (voice) => {
  try {
    const response = await fetch(`${API_BASE_URL}/sessions`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ voice })
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to create session - ${response.status}: ${errorText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Error creating session:', error);
    throw error;
  }
};

export const connectRTC = async (sdp, ephemeralKey, deploymentName, region) => {
  try {
    const response = await fetch(`${API_BASE_URL}/rtc`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ 
        sdp, 
        ephemeralKey,
        deploymentName,
        region
      })
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`RTC connect failed - ${response.status}: ${errorText}`);
    }

    return await response.text();
  } catch (error) {
    console.error('Error connecting RTC:', error);
    throw error;
  }
};