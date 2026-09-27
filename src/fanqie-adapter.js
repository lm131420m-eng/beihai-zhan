(() => {
  if (window.beihaiAdapter) return;
  const css = `
html[data-beihai-theme][data-theme] body.windows-shell {
  --m3-primary:#6c54d8;--m3-on-primary:#fff;--m3-primary-container:#eee9fb;--m3-on-primary-container:#5840bd;
  --m3-secondary-container:#eee9fb;--m3-on-secondary-container:#6c54d8;
  --m3-tertiary-container:#eee9fb;--m3-on-tertiary-container:#5840bd;
  --m3-surface:#f7f8fc;--m3-on-surface:#26253d;--m3-on-surface-variant:#77758b;
  --m3-surface-container-lowest:#fff;--m3-surface-container-low:#fff;--m3-surface-container:#fff;
  --m3-surface-container-high:#f0edf8;--m3-surface-container-highest:#e9e4f5;
  --m3-outline:#9690a8;--m3-outline-variant:#e9e7ef;
  --m3-shape-sm:8px;--m3-shape-md:10px;--m3-shape-lg:14px;--m3-shape-full:10px;
  --bg-0:var(--m3-surface);--bg-1:var(--m3-surface-container-low);--bg-2:var(--m3-surface-container);
  --panel:var(--m3-surface-container-low);--panel-2:var(--m3-surface-container);--panel-3:var(--m3-surface-container-high);
  --text:var(--m3-on-surface);--text-2:var(--m3-on-surface-variant);--text-3:var(--m3-on-surface-variant);
  --accent:var(--m3-primary);--accent-text:var(--m3-primary);--on-accent:#fff;
  --line:var(--m3-outline-variant);--line-strong:var(--m3-outline);
  background:var(--m3-surface)!important;color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif;
}
html[data-beihai-theme="dark"][data-theme] body.windows-shell {
  --m3-primary:#9d85ef;--m3-on-primary:#17171f;--m3-primary-container:#302b48;--m3-on-primary-container:#c4b3ff;
  --m3-secondary-container:#302b48;--m3-on-secondary-container:#c4b3ff;
  --m3-tertiary-container:#302b48;--m3-on-tertiary-container:#c4b3ff;
  --m3-surface:#17171f;--m3-on-surface:#e9e7f2;--m3-on-surface-variant:#aaa6be;
  --m3-surface-container-lowest:#17171f;--m3-surface-container-low:#22222e;--m3-surface-container:#22222e;
  --m3-surface-container-high:#302b48;--m3-surface-container-highest:#393348;
  --m3-outline:#91889f;--m3-outline-variant:#353344;
}
html[data-beihai-theme] {background:var(--beihai-bg,#f7f8fc)!important}
html[data-beihai-theme] .app-shell {
  width:100%!important;max-width:none!important;margin:0!important;padding:18px!important;
  grid-template-areas:"navigation" "announcement" "content" "jobs"!important;
  grid-template-rows:auto auto minmax(0,1fr) auto!important;gap:14px!important;
}
.app-shell > .topbar,#windowControls,#themeToggle,.reader-window-controls {display:none!important}
html[data-beihai-theme] .tabbar {background:var(--panel);border:1px solid var(--line);padding:8px 12px;border-radius:12px}
html[data-beihai-theme] .nav-item.active {background:var(--m3-secondary-container);color:var(--m3-on-secondary-container)}
html[data-beihai-theme] .nav-item.active .nav-icon {background:transparent;color:inherit}
html[data-beihai-theme] .panel-shell {border:1px solid var(--line)}
html[data-beihai-theme] .accent-button,html[data-beihai-theme] .progress-bar {background:var(--m3-primary)!important;color:var(--m3-on-primary)!important}
html[data-beihai-theme] .text-input:focus {outline-color:var(--m3-primary)}
html[data-beihai-theme] #topbarUpdateCard {display:flex;justify-content:flex-start;flex-wrap:wrap;gap:14px;width:100%;padding:14px;margin-bottom:12px;border-radius:10px;background:var(--panel-3)}
html[data-beihai-theme] #topbarUpdateValue {white-space:normal;overflow:visible;text-overflow:clip}
html[data-beihai-theme] #topbarUpdateCard > :first-child {flex:1;min-width:180px}
html[data-beihai-theme] .settings-card.beihai-update-card {grid-column:1/-1}
@media(max-width:760px){html[data-beihai-theme] .app-shell{padding:10px!important;gap:10px!important}}
`;
  let desiredTheme='light';
  const style=document.createElement('style');style.id='beihai-fanqie-style';style.textContent=css;
  function mount() {
    if(!document.head||!document.body)return;
    if(!style.isConnected)document.head.append(style);
    const check=document.getElementById('settingsCheckUpdateButton');
    const card=check?.closest('.settings-card');
    const update=document.getElementById('topbarUpdateCard');
    if(card&&update&&!card.contains(update)) {
      card.classList.add('beihai-update-card');
      card.querySelector('.field-label').textContent='番茄下载器 · 版本与更新';
      check.closest('.field').insertBefore(update,check.closest('.button-row'));
      card.parentElement.prepend(card);
    }
    document.documentElement.dataset.beihaiTheme=desiredTheme;
    document.documentElement.style.setProperty('--beihai-bg',desiredTheme==='dark'?'#17171f':'#f7f8fc');
    // Follow the host for application chrome only; leave saved reader themes intact.
    if(document.documentElement.dataset.theme!==desiredTheme)document.documentElement.dataset.theme=desiredTheme;
  }
  window.beihaiAdapter={
    theme(value){desiredTheme=value==='dark'?'dark':'light';mount();},
    settings(checkUpdates){
      mount();document.querySelector('#navList [data-view="settings"]')?.click();
      const check=document.getElementById('settingsCheckUpdateButton');
      check?.closest('.settings-card')?.scrollIntoView({block:'start'});
      if(checkUpdates&&!check?.disabled)check?.click();
      return !!check;
    }
  };
  mount();
  addEventListener('DOMContentLoaded',mount,{once:true});
  // Startup may rebuild native theme state; reapply only when it changes.
  new MutationObserver(()=>{if(document.documentElement.dataset.theme!==desiredTheme)mount();}).observe(document.documentElement,{attributes:true,attributeFilter:['data-theme']});
  if(!document.getElementById('settingsCheckUpdateButton'))addEventListener('load',mount,{once:true});
})();
