let peerConnection = null;
let signalingSocket = null;
let currentRotation = 0;
let isMuted = false;

const idleScreen = document.getElementById('idleScreen');
const streamScreen = document.getElementById('streamScreen');
const remoteVideo = document.getElementById('remoteVideo');
const ipDisplay = document.getElementById('ipDisplay');
const directUrlInput = document.getElementById('directUrlInput');
const copyBtn = document.getElementById('copyBtn');
const resBadge = document.getElementById('resBadge');
const fpsBadge = document.getElementById('fpsBadge');
const rotateBtn = document.getElementById('rotateBtn');
const screenshotBtn = document.getElementById('screenshotBtn');
const fullscreenBtn = document.getElementById('fullscreenBtn');
const muteBtn = document.getElementById('muteBtn');
const disconnectBtn = document.getElementById('disconnectBtn');

window.addEventListener('DOMContentLoaded', async () => {
  await fetchStatusAndSetupQR();
  connectSignaling();
  setupUIEvents();
});

async function fetchStatusAndSetupQR() {
  try {
    const res = await fetch('/api/status');
    const data = await res.json();
    
    if (ipDisplay) ipDisplay.textContent = `${data.localIp}`;
    if (directUrlInput) directUrlInput.value = data.senderUrl;

    const qrWrapper = document.getElementById('qrCodeWrapper');
    if (qrWrapper && window.QRCode) {
      qrWrapper.innerHTML = '';
      new QRCode(qrWrapper, {
        text: data.senderUrl,
        width: 108,
        height: 108,
        colorDark: '#000000',
        colorLight: '#ffffff',
        correctLevel: QRCode.CorrectLevel.M
      });
    }
  } catch (err) {
    if (ipDisplay) ipDisplay.textContent = 'offline';
  }
}

function connectSignaling() {
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const wsUrl = `${protocol}//${window.location.host}/ws?role=receiver`;
  
  signalingSocket = new WebSocket(wsUrl);

  signalingSocket.onmessage = async (event) => {
    const msg = JSON.parse(event.data);
    switch (msg.type) {
      case 'offer':
        await handleOffer(msg.offer);
        break;
      case 'candidate':
        await handleCandidate(msg.candidate);
        break;
      case 'sender-left':
        handleSenderDisconnected();
        break;
    }
  };

  signalingSocket.onclose = () => {
    setTimeout(connectSignaling, 2000);
  };
}

async function handleOffer(offer) {
  if (peerConnection) {
    peerConnection.close();
  }

  peerConnection = new RTCPeerConnection({
    iceServers: []
  });

  peerConnection.ontrack = (event) => {
    if (event.streams && event.streams[0]) {
      remoteVideo.srcObject = event.streams[0];
      switchToStreamView();
    }
  };

  peerConnection.onicecandidate = (event) => {
    if (event.candidate && signalingSocket?.readyState === WebSocket.OPEN) {
      signalingSocket.send(JSON.stringify({
        type: 'candidate',
        candidate: event.candidate
      }));
    }
  };

  await peerConnection.setRemoteDescription(new RTCSessionDescription(offer));
  const answer = await peerConnection.createAnswer();
  await peerConnection.setLocalDescription(answer);

  signalingSocket.send(JSON.stringify({
    type: 'answer',
    answer: answer
  }));
}

async function handleCandidate(candidate) {
  if (peerConnection) {
    try {
      await peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
    } catch (e) {
      console.warn('ICE error', e);
    }
  }
}

function switchToStreamView() {
  idleScreen.classList.add('hidden');
  streamScreen.classList.remove('hidden');
  currentRotation = 0;
  applyVideoRotation();

  remoteVideo.onloadedmetadata = () => {
    resBadge.textContent = `${remoteVideo.videoWidth}x${remoteVideo.videoHeight}`;
  };
}

function handleSenderDisconnected() {
  if (remoteVideo.srcObject) {
    remoteVideo.srcObject.getTracks().forEach(track => track.stop());
    remoteVideo.srcObject = null;
  }
  if (peerConnection) {
    peerConnection.close();
    peerConnection = null;
  }
  streamScreen.classList.add('hidden');
  idleScreen.classList.remove('hidden');
}

function setupUIEvents() {
  const openMiracastBtn = document.getElementById('openMiracastBtn');
  if (openMiracastBtn) {
    openMiracastBtn.addEventListener('click', async () => {
      openMiracastBtn.textContent = 'Opening Settings...';
      try {
        await fetch('/api/launch-miracast', { method: 'POST' });
      } catch (e) {
        console.error(e);
      } finally {
        setTimeout(() => {
          openMiracastBtn.textContent = 'Open Windows Wireless Display';
        }, 1500);
      }
    });
  }

  if (copyBtn && directUrlInput) {
    copyBtn.addEventListener('click', () => {
      navigator.clipboard.writeText(directUrlInput.value);
      copyBtn.textContent = 'Copied';
      setTimeout(() => copyBtn.textContent = 'Copy', 1500);
    });
  }

  rotateBtn.addEventListener('click', () => {
    currentRotation = (currentRotation + 90) % 360;
    applyVideoRotation();
  });

  screenshotBtn.addEventListener('click', () => {
    if (!remoteVideo.videoWidth) return;
    const canvas = document.createElement('canvas');
    canvas.width = remoteVideo.videoWidth;
    canvas.height = remoteVideo.videoHeight;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(remoteVideo, 0, 0, canvas.width, canvas.height);
    
    const link = document.createElement('a');
    link.download = `Screen_${Date.now()}.png`;
    link.href = canvas.toDataURL('image/png');
    link.click();
  });

  fullscreenBtn.addEventListener('click', () => {
    const container = document.getElementById('videoContainer');
    if (!document.fullscreenElement) {
      container.requestFullscreen?.() || document.documentElement.requestFullscreen();
    } else {
      document.exitFullscreen?.();
    }
  });

  muteBtn.addEventListener('click', () => {
    isMuted = !isMuted;
    remoteVideo.muted = isMuted;
    muteBtn.style.opacity = isMuted ? '0.4' : '1';
  });

  disconnectBtn.addEventListener('click', () => {
    if (signalingSocket?.readyState === WebSocket.OPEN) {
      signalingSocket.send(JSON.stringify({ type: 'stop-stream' }));
    }
    handleSenderDisconnected();
  });

  window.addEventListener('keydown', (e) => {
    if (e.key === 'F11') {
      e.preventDefault();
      fullscreenBtn.click();
    }
  });
}

function applyVideoRotation() {
  remoteVideo.style.transform = `rotate(${currentRotation}deg)`;
}
