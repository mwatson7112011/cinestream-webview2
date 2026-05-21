// Polyfill electronAPI for WebView2 context
window.electronAPI = {
  platform: 'win32',
  environment: 'production',
  logToTerminal: (message) => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({ type: 'log', message: message }));
    } else {
      console.log(message);
    }
  },
  openPopup: (url, partition) => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({ type: 'openPopup', url: url, partition: partition }));
    } else {
      window.open(url, '_blank');
    }
  },
  getWebviewPreloadPath: () => ''
};
