const fs = require('fs');
const path = require('path');

const project = path.resolve(__dirname, '..');
const release = path.join(project, 'output', 'beihaiZ');
const appHtml = path.join(release, '北海.html');
const stage = path.join(__dirname, 'staging-ntr-workflow');
const target = path.join(release, '资料（勿删）', '04_写作辅助', '追夫绿帽创作工作流');

if (!fs.existsSync(appHtml)) throw new Error('北海.html 不存在');
if (!fs.existsSync(stage)) throw new Error('暂存资料不存在');
fs.mkdirSync(target, { recursive: true });

for (const name of fs.readdirSync(stage)) {
  fs.copyFileSync(path.join(stage, name), path.join(target, name));
}
fs.copyFileSync(
  'C:/Users/Administrator/Desktop/追夫工作流，全放进去即可【一键放毒】.zip',
  path.join(target, '追夫工作流原始资料.zip')
);

const brainName = '写脑洞·NTR绿帽题材炸裂脑洞库.html';
let brain = fs.readFileSync(path.join(target, brainName), 'utf8');
const purple = `<style id="beihai-purple-theme">
:root{--ac:#6f52df;--ac2:#5a3fc7;--acbg:#f0ebff;--ok:#6f52df;--okbg:#f0ebff}
html[data-theme="dark"]{--ac:#a68bfa;--ac2:#c4b5fd;--acbg:#30284a;--ok:#a68bfa;--okbg:#30284a}
</style>`;
if (!brain.includes('beihai-purple-theme')) brain = brain.replace('</head>', purple + '</head>');
fs.writeFileSync(path.join(target, brainName), brain, 'utf8');

const textNames = [
  '绿帽工作流加强测试版.txt',
  '追夫工作流更新.txt',
  '调用库.txt',
  '发知乎的调用库，知乎都是知识分子男性【放毒效果更好】.txt',
  '可视化思维导图.txt'
];
const labels = ['绿帽工作流', '追夫工作流', '通用调用库', '知乎调用库', '思维导图文本'];
const docs = textNames.map((name, i) => ({ name, label: labels[i], text: fs.readFileSync(path.join(target, name), 'utf8') }));

const wrapper = `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>追夫·绿帽创作工作流</title><style>
*{box-sizing:border-box}html,body{margin:0;height:100%;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI","Microsoft YaHei",sans-serif;background:#f7f7fb;color:#262338}body{display:flex;flex-direction:column}.tabs{display:flex;gap:7px;padding:12px 14px;border-bottom:1px solid #e8e5f0;background:#fff;overflow-x:auto;flex:none}.tab{border:1px solid #e2ddef;background:#fff;color:#6f687b;border-radius:8px;padding:8px 13px;white-space:nowrap;cursor:pointer;font-size:13px}.tab:hover,.tab.active{background:#6f52df;border-color:#6f52df;color:#fff}.actions{margin-left:auto;display:flex;gap:7px}.panel{display:none;min-height:0;flex:1}.panel.active{display:flex;flex-direction:column}.brain{width:100%;height:100%;border:0;background:#fff}.docbar{display:flex;align-items:center;gap:10px;padding:12px 18px;border-bottom:1px solid #e8e5f0;background:#fff}.docbar h2{font-size:16px;margin:0}.copy{margin-left:auto;border:0;border-radius:8px;padding:8px 14px;background:#6f52df;color:#fff;cursor:pointer}.content{margin:0;padding:22px 26px 60px;white-space:pre-wrap;word-break:break-word;line-height:1.85;font-family:inherit;font-size:15px;overflow:auto}.map{padding:22px;overflow:auto;align-items:center}.map img{display:block;max-width:100%;height:auto;margin:auto;border-radius:12px;box-shadow:0 6px 28px #1f17351c}.hint{color:#8a8398;font-size:12px}.toast{position:fixed;right:18px;bottom:18px;background:#262338;color:#fff;padding:9px 14px;border-radius:8px;opacity:0;transform:translateY(8px);transition:.2s;pointer-events:none}.toast.show{opacity:1;transform:none}
html[data-theme="dark"] body{background:#17171f;color:#e9e7f2}html[data-theme="dark"] .tabs,html[data-theme="dark"] .docbar{background:#22222e;border-color:#3b3648}html[data-theme="dark"] .tab{background:#292735;border-color:#40394f;color:#c7c0d2}html[data-theme="dark"] .tab:hover,html[data-theme="dark"] .tab.active{background:#8063e8;border-color:#8063e8;color:#fff}html[data-theme="dark"] .brain{background:#17171f}html[data-theme="dark"] .hint{color:#aaa3ba}
@media(max-width:700px){.tabs{padding:9px}.tab{padding:7px 10px}.content{padding:17px 15px;font-size:14px}.actions{display:none}}
</style></head><body><div class="tabs" id="tabs"><button class="tab active" data-panel="brain">脑洞库</button>${docs.map((d,i)=>`<button class="tab" data-panel="doc${i}">${d.label}</button>`).join('')}<button class="tab" data-panel="map">引擎脑图</button><span class="actions"><span class="hint">全部资料已内置，可直接查看和复制</span></span></div><section class="panel active" id="brain"><iframe class="brain" id="brain-frame" src="./${encodeURIComponent(brainName)}" title="NTR绿帽题材炸裂脑洞库"></iframe></section>${docs.map((d,i)=>`<section class="panel" id="doc${i}"><div class="docbar"><div><h2>${d.label}</h2><span class="hint">${d.name}</span></div><button class="copy" data-copy="${i}">复制全文</button></div><pre class="content"></pre></section>`).join('')}<section class="panel map" id="map"><img src="./${encodeURIComponent('追夫火葬场引擎v2.0进化版_ima脑图.jpeg')}" alt="追夫火葬场引擎思维导图"></section><div class="toast" id="toast">已复制</div><script>
const DOCS=${JSON.stringify(docs)};document.querySelectorAll('.content').forEach((el,i)=>el.textContent=DOCS[i].text);const tabs=document.getElementById('tabs');tabs.onclick=e=>{const b=e.target.closest('[data-panel]');if(!b)return;document.querySelectorAll('.tab').forEach(x=>x.classList.toggle('active',x===b));document.querySelectorAll('.panel').forEach(x=>x.classList.toggle('active',x.id===b.dataset.panel));};document.querySelectorAll('[data-copy]').forEach(b=>b.onclick=async()=>{const text=DOCS[Number(b.dataset.copy)].text;try{await navigator.clipboard.writeText(text)}catch{const t=document.createElement('textarea');t.value=text;document.body.append(t);t.select();document.execCommand('copy');t.remove()}const toast=document.getElementById('toast');toast.classList.add('show');setTimeout(()=>toast.classList.remove('show'),1200)});function sync(){const dark=document.documentElement.dataset.theme==='dark',f=document.getElementById('brain-frame');try{if(f.contentDocument)f.contentDocument.documentElement.dataset.theme=dark?'dark':'light'}catch{}}document.getElementById('brain-frame').addEventListener('load',sync);new MutationObserver(sync).observe(document.documentElement,{attributes:true,attributeFilter:['data-theme']});
</script></body></html>`;
fs.writeFileSync(path.join(target, '追夫绿帽创作工作流.html'), wrapper, 'utf8');

let html = fs.readFileSync(appHtml, 'utf8');
if (!fs.existsSync(path.join(__dirname, 'backup-before-ntr-module.html'))) fs.copyFileSync(appHtml, path.join(__dirname, 'backup-before-ntr-module.html'));

if (!html.includes('id="ntr-workflow-nav"')) {
  html = html.replace(
    '<button class="nav-btn" id="generator-nav" aria-pressed="false"><span style="width:20px;text-align:center;font-size:18px">✦</span>导言生成<span class="count">598</span></button>',
    '<button class="nav-btn" id="generator-nav" aria-pressed="false"><span style="width:20px;text-align:center;font-size:18px">✦</span>导言生成<span class="count">598</span></button><button class="nav-btn" id="ntr-workflow-nav" aria-pressed="false"><span style="width:20px;text-align:center;font-size:18px">◆</span>追夫·绿帽工作流<span class="count">7</span></button>'
  );
  const section = '<section class="generator-view ntr-workflow-view" id="ntr-workflow-view" hidden aria-labelledby="ntr-workflow-heading"><div class="generator-top"><div><h2 id="ntr-workflow-heading">追夫 · 绿帽创作工作流</h2><p>炸裂脑洞库、绿帽逆袭工作流、追夫火葬场引擎与调用资料</p></div><button class="soft-btn" id="back-ntr-library">← 返回资料库</button></div><iframe class="generator-frame" id="ntr-workflow-frame" title="追夫绿帽创作工作流" allow="clipboard-write"></iframe></section>';
  html = html.replace('<section class="platform-guide-view" id="platform-guide-view"', section + '<section class="platform-guide-view" id="platform-guide-view"');
  html = html.replace('</body>', `<script id="ntr-workflow-script">(()=>{const nav=document.getElementById('ntr-workflow-nav'),view=document.getElementById('ntr-workflow-view'),frame=document.getElementById('ntr-workflow-frame'),library=document.getElementById('library-main'),generator=document.getElementById('generator-view'),guide=document.getElementById('platform-guide-view');let loaded=false;const resource='资料（勿删）/04_写作辅助/追夫绿帽创作工作流/追夫绿帽创作工作流.html'.split('/').map(encodeURIComponent).join('/');function syncTheme(){try{if(frame.contentDocument)frame.contentDocument.documentElement.dataset.theme=document.documentElement.dataset.theme==='dark'?'dark':'light'}catch{}}function close(){view.hidden=true;nav.classList.remove('active');nav.setAttribute('aria-pressed','false')}function open(){library.hidden=true;generator.hidden=true;if(guide)guide.hidden=true;view.hidden=false;document.querySelectorAll('#library .nav-btn,#platform-guide-nav,#generator-nav').forEach(b=>{b.classList.remove('active');b.setAttribute('aria-pressed','false')});nav.classList.add('active');nav.setAttribute('aria-pressed','true');if(!loaded){frame.src=resource;loaded=true}syncTheme();window.scrollTo(0,0)}nav.onclick=open;document.getElementById('back-ntr-library').onclick=()=>{close();library.hidden=false;if(typeof window.showPlatformGuide==='function')window.showPlatformGuide(false)};document.getElementById('library').addEventListener('click',close,true);document.getElementById('generator-nav').addEventListener('click',close,true);document.getElementById('platform-guide-nav').addEventListener('click',close,true);frame.addEventListener('load',syncTheme);new MutationObserver(syncTheme).observe(document.documentElement,{attributes:true,attributeFilter:['data-theme']});const oldSwitch=window.switchCollection;window.switchCollection=function(name){close();nav.hidden=name!=='writing';nav.previousElementSibling.hidden=name!=='writing';return oldSwitch.apply(this,arguments)};})();</script></body>`);
}

fs.writeFileSync(appHtml, html, 'utf8');
console.log(JSON.stringify({ target, wrapperBytes: Buffer.byteLength(wrapper), appBytes: Buffer.byteLength(html) }, null, 2));
