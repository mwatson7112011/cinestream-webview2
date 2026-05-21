// Webview Guest Preload Script
// This script runs in the context of the guest page (e.g., Paramount+, YouTube) at document-start, before any page scripts load.

console.log('[CineStream Preload] Guest preload script initializing...');

(function() {
  try {
    // 1. Listen for guest unhandled script exceptions to log them to the terminal
    window.addEventListener('error', (event) => {
      console.error(`[CineStream Guest JS Error] ${event.message} at ${event.filename}:${event.lineno}:${event.colno}`);
    });

    window.addEventListener('unhandledrejection', (event) => {
      console.error(`[CineStream Guest Promise Rejection] ${event.reason}`);
    });

    // 2. Inject overrides directly into the main world to execute before the site's own scripts load
    const script = document.createElement('script');
    script.textContent = `
      (function() {
        try {
          // Suppress navigator.webdriver to bypass automation checks (critical for Google login)
          try {
            if (navigator.webdriver !== undefined) {
              Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined,
                configurable: true
              });
            }
          } catch (e) {
            console.error('[CineStream Preload] Failed to override navigator.webdriver:', e);
          }

          // Mock navigator.userAgentData to return standard Chrome brands instead of Chromium-only
          if (navigator.userAgentData) {
            try {
              const uaMatch = navigator.userAgent.match(/Chrome\\/(\\d+)\\./);
              const majorVer = uaMatch ? uaMatch[1] : '132';
              
              const mockBrands = [
                { brand: 'Not A(Brand', version: '99' },
                { brand: 'Google Chrome', version: majorVer },
                { brand: 'Chromium', version: majorVer }
              ];
              
              Object.defineProperty(navigator.userAgentData, 'brands', {
                get: () => mockBrands,
                configurable: true
              });
              
              Object.defineProperty(navigator.userAgentData, 'getHighEntropyValues', {
                value: async (hints) => {
                  const values = {};
                  if (hints.includes('brands')) values.brands = mockBrands;
                  if (hints.includes('mobile')) values.mobile = false;
                  if (hints.includes('platform')) {
                    let platform = 'Windows';
                    if (navigator.userAgent.includes('Macintosh')) platform = 'macOS';
                    else if (navigator.userAgent.includes('Linux')) platform = 'Linux';
                    values.platform = platform;
                  }
                  if (hints.includes('platformVersion')) values.platformVersion = '10.0.0';
                  if (hints.includes('architecture')) values.architecture = 'x86';
                  if (hints.includes('model')) values.model = '';
                  if (hints.includes('uaFullVersion')) {
                    const fullMatch = navigator.userAgent.match(/Chrome\\/([0-9.]+)/);
                    values.uaFullVersion = fullMatch ? fullMatch[1] : '132.0.0.0';
                  }
                  return values;
                },
                configurable: true
              });
              console.log('[CineStream Preload] Overrode userAgentData brands for Google Chrome compatibility.');
            } catch (err) {
              console.error('[CineStream Preload] Error setting up userAgentData override:', err);
            }
          }

          // Intercept XMLHttpRequest.prototype.getResponseHeader
          const originalGetHeader = XMLHttpRequest.prototype.getResponseHeader;
          XMLHttpRequest.prototype.getResponseHeader = function(header) {
            const val = originalGetHeader.apply(this, arguments);
            // If New Relic looks for its app tracing data and it is null or undefined, return an empty string to prevent split() crashes.
            if (header && header.toLowerCase() === 'x-newrelic-app-data' && (val === null || val === undefined)) {
              return '';
            }
            return val;
          };

          // Intercept Headers.prototype.get for fetch API calls
          const originalHeadersGet = Headers.prototype.get;
          Headers.prototype.get = function(header) {
            const val = originalHeadersGet.apply(this, arguments);
            if (header && header.toLowerCase() === 'x-newrelic-app-data' && (val === null || val === undefined)) {
              return '';
            }
            return val;
          };

          // Host message communication for hover-reveal sidebar is handled natively via WPF cursor polling.

          console.log('[CineStream Preload] Telemetry and automation patches successfully injected.');
        } catch (e) {
          console.error('[CineStream Preload] Error setting up main world overrides:', e);
        }
      })();
    `;
    (document.head || document.documentElement).appendChild(script);
    script.remove();
  } catch (err) {
    console.error('[CineStream Preload] Failed to run guest preload injector:', err);
  }
})();
