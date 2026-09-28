const fs = require('fs');
const p = 'output/beihaiZ/北海.html';
let s = fs.readFileSync(p, 'utf8');
const replacements = [
  [
    '.main{padding:30px 38px 36px;min-width:0;max-width:1600px;width:100%;margin:0 auto}',
    '.main{padding:30px 38px 36px;min-width:0;max-width:1600px;width:100%;margin:auto}.main.collection-top{margin:0 auto auto}'
  ],
  [
    '#main-collections button{padding:10px 22px;border-radius:9px;background:transparent;color:var(--muted);font-weight:650;font-size:15px}',
    '#main-collections button{width:104px;padding:10px 12px;border-radius:9px;background:transparent;color:var(--muted);font-weight:650;font-size:15px;white-space:nowrap;text-align:center}'
  ],
  [
    '@media(max-width:900px){#main-collections{gap:2px;margin-left:12px}#main-collections button{padding:9px 12px}',
    '@media(max-width:900px){#main-collections{gap:2px;margin-left:12px}#main-collections button{width:88px;padding:9px 8px}'
  ],
  [
    '@media(max-width:760px){#main-collections{margin-left:8px;gap:2px;overflow-x:auto}#main-collections button{padding:8px;font-size:12px}',
    '@media(max-width:760px){#main-collections{margin-left:8px;gap:2px;overflow-x:auto}#main-collections button{width:auto;min-width:70px;padding:8px;font-size:12px}'
  ]
];
for (const [from, to] of replacements) {
  if (s.includes(from)) s = s.replace(from, to);
  else if (!s.includes(to)) throw new Error('layout marker not found: ' + from);
}
const sidebarPlatforms = '<div class="side-title">平台与来源</div><nav id="platforms" aria-label="平台与来源"></nav>';
const hiddenSidebarPlatforms = '<div class="side-title" id="sidebar-platform-title" hidden>平台与来源</div><nav id="platforms" aria-label="平台与来源" hidden></nav>';
if (s.includes(sidebarPlatforms)) s = s.replace(sidebarPlatforms, hiddenSidebarPlatforms);
const removedSidebarPlatforms = '<nav id="platforms" aria-label="平台与来源" hidden></nav>';
if (!s.includes(hiddenSidebarPlatforms) && !s.includes(removedSidebarPlatforms)) throw new Error('sidebar platform marker not found');
if (s.includes(hiddenSidebarPlatforms)) s = s.replace(hiddenSidebarPlatforms, removedSidebarPlatforms);
else if (!s.includes(removedSidebarPlatforms)) throw new Error('hidden sidebar platform marker not found');

const oldGeneratorNav = '<button class="nav-btn" id="generator-nav" aria-pressed="false"><span style="width:20px;text-align:center;font-size:18px">✦</span>书名·导言生成器<span class="count">598</span></button>';
const renamedGeneratorNav = '<button class="nav-btn" id="generator-nav" aria-pressed="false"><span style="width:20px;text-align:center;font-size:18px">✦</span>导言生成<span class="count">598</span></button>';
if (s.includes(oldGeneratorNav)) s = s.replace(oldGeneratorNav, renamedGeneratorNav);
else if (!s.includes(renamedGeneratorNav)) throw new Error('generator navigation marker not found');

const positionalPlatformVisibility = "$('platforms').hidden=fanqie;$('platforms').previousElementSibling.hidden=fanqie;";
const explicitPlatformVisibility = "$('platforms').hidden=fanqie;const platformTitle=$('sidebar-platform-title');if(platformTitle)platformTitle.hidden=fanqie;";
if (s.includes(positionalPlatformVisibility)) s = s.replace(positionalPlatformVisibility, explicitPlatformVisibility);
else if (!s.includes(explicitPlatformVisibility)) throw new Error('platform visibility marker not found');
const positionalPlatformTitle = "$('platforms').previousElementSibling.textContent=activeCollection==='writing'?'平台分类':'平台与来源';";
const explicitPlatformTitle = "const platformTitle=$('sidebar-platform-title');if(platformTitle)platformTitle.textContent=activeCollection==='writing'?'平台分类':'平台与来源';";
if (s.includes(positionalPlatformTitle)) s = s.replace(positionalPlatformTitle, explicitPlatformTitle);
else if (!s.includes(explicitPlatformTitle)) throw new Error('platform title marker not found');

const oldFilters = '<div id="filters" class="filters" aria-label="题材筛选"></div>';
const groupedFilters = '<div id="writing-filter-panel" class="writing-filter-panel" hidden><div class="filter-group" id="platform-filter-group"><span class="filter-label">小说平台</span><div id="platform-filters" class="filters" aria-label="小说平台筛选"></div></div><div class="filter-group" id="genre-filter-group"><span class="filter-label">小说类型</span><div id="filters" class="filters" aria-label="小说类型筛选"></div></div></div>';
if (s.includes(oldFilters)) s = s.replace(oldFilters, groupedFilters);
else if (!s.includes(groupedFilters)) throw new Error('content filter marker not found');

const collectionStyleEnd = '.fanqie-tool{padding:20px}}</style>';
const filterStyles = '.fanqie-tool{padding:20px}}#platforms{display:none!important}.writing-filter-panel{margin:2px 0 21px;padding:15px 18px 7px;background:#fff;border:1px solid var(--line);border-radius:12px}.filter-group{display:grid;grid-template-columns:78px minmax(0,1fr);align-items:start;gap:10px;margin-bottom:8px}.filter-label{color:var(--muted);font-size:12px;font-weight:650;line-height:30px;white-space:nowrap}.filter-group .filters{margin:0;gap:5px}.filter-group .pill{padding:5px 10px}:root[data-theme="dark"] .writing-filter-panel{background:#22222e}@media(max-width:760px){.filter-group{grid-template-columns:1fr;gap:2px}.filter-label{line-height:24px}}</style>';
if (s.includes(collectionStyleEnd)) s = s.replace(collectionStyleEnd, filterStyles);
else if (!s.includes('.writing-filter-panel{margin:2px 0 21px')) throw new Error('filter style marker not found');
if (!s.includes('#platforms{display:none!important}')) s = s.replace('.writing-filter-panel{margin:2px 0 21px','#platforms{display:none!important}.writing-filter-panel{margin:2px 0 21px');
const switchStart = "window.switchCollection=function(collection){\n if(!names[collection])return;";
const guardedSwitchStart = "let fanqieCommit=false;\nwindow.switchCollection=function(collection){\n if(!names[collection])return;\n if(collection==='fanqie'&&!fanqieCommit){window.chrome?.webview?.postMessage('prepare-fanqie');return;}";
if (!s.includes(guardedSwitchStart) && s.includes(switchStart)) s = s.replace(switchStart, guardedSwitchStart);
else if (!s.includes(guardedSwitchStart)) throw new Error('fanqie switch start marker not found');
const switchEnd = " if(window.chrome?.webview)window.chrome.webview.postMessage(fanqie?'embed-fanqie':'hide-fanqie');\n};";
const guardedSwitchEnd = " if(window.chrome?.webview&&!fanqie)window.chrome.webview.postMessage('hide-fanqie');\n};\nwindow.commitFanqieCollection=function(){fanqieCommit=true;try{switchCollection('fanqie')}finally{fanqieCommit=false}};";
if (s.includes(switchEnd)) s = s.replace(switchEnd, guardedSwitchEnd);
else if (!s.includes(guardedSwitchEnd)) throw new Error('fanqie switch end marker not found');
// Keep short collection pages aligned with Writing without changing Writing's layout.
const collectionPositionMarker = "window.activeCollection=collection;";
const collectionPositionFix = "window.activeCollection=collection;\n document.querySelector('.main').classList.toggle('collection-top',collection==='scripts'||collection==='aigc');";
if (!s.includes(collectionPositionFix)) {
  if (s.includes(collectionPositionMarker)) s = s.replace(collectionPositionMarker, collectionPositionFix);
  else throw new Error('collection position marker not found');
}
const fanqieRender = "render=function(){dataRender();if(activeCollection==='fanqie'){$('cards').innerHTML='<section class=\"fanqie-tool\"";
const filterHook = `const collectionFilterRender=render;
render=function(){
 collectionFilterRender();
 const showWritingFilters=activeCollection==='writing'&&state.category==='stories';
 $('writing-filter-panel').hidden=!showWritingFilters;
 if(!showWritingFilters)return;
 const values=platformNames();
 $('platform-filters').innerHTML=['',...values].map(p=>\`<button class="pill \${state.platform===p?'active':''}" data-content-platform="\${esc(p)}" aria-pressed="\${state.platform===p}">\${p?esc(p.replace('_',' · ')):'全部平台'}</button>\`).join('');
};
$('platform-filters').onclick=e=>{const b=e.target.closest('[data-content-platform]');if(!b)return;state.platform=b.dataset.contentPlatform;state.category='stories';state.genre='全部分类';state.page=1;nav();render()};
`;
if (!s.includes('const collectionFilterRender=render;')) {
  const hookAt = "document.addEventListener('click',e=>{if(e.target.closest('[data-fanqie-open]'))";
  if (s.includes(hookAt)) s = s.replace(hookAt, filterHook + hookAt);
  else throw new Error('collection filter hook marker not found');
}
// Repair older non-idempotent builds that inserted the Fanqie guard twice.
s = s.replace(/(?:let fanqieCommit=false;\s*){2,}/g, 'let fanqieCommit=false;\n');
s = s.replace(/(?: if\(collection==='fanqie'&&!fanqieCommit\)\{window\.chrome\?\.webview\?\.postMessage\('prepare-fanqie'\);return;\}\s*){2,}/g, " if(collection==='fanqie'&&!fanqieCommit){window.chrome?.webview?.postMessage('prepare-fanqie');return;}\n");
fs.writeFileSync(p, s);

