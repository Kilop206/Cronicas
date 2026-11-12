// Keep recovery independent of Angular and its hashed chunks. No automatic reload loop.
(() => {
  const show = () => {
    const fallback = document.getElementById('boot-failure');
    if (fallback) fallback.hidden = false;
  };
  window.addEventListener('error', event => {
    const target = event.target;
    if (target instanceof HTMLScriptElement && target.type === 'module') show();
  }, true);
  window.setTimeout(() => { if (!document.querySelector('app-root router-outlet')) show(); }, 30000);
  document.getElementById('boot-reload')?.addEventListener('click', () => window.location.reload());
})();
