const fs = require('fs');
const path = require('path');

const release = path.resolve(__dirname, '../output/beihaiZ');
const appPath = path.join(release, '北海.html');
const moduleDir = path.join(release, '资料（勿删）', '04_写作辅助', '追夫绿帽创作工作流');
const wrapperPath = path.join(moduleDir, '追夫绿帽创作工作流.html');

let wrapper = fs.readFileSync(wrapperPath, 'utf8');
wrapper = wrapper
  .replace(/<button class="tab" data-panel="doc4">思维导图文本<\/button>/, '')
  .replace(/<button class="tab" data-panel="map">引擎脑图<\/button>/, '')
  .replace(/<section class="panel" id="doc4">[\s\S]*?<\/section>/, '')
  .replace(/<section class="panel map" id="map">[\s\S]*?<\/section>/, '');

const docsMatch = wrapper.match(/const DOCS=(\[[\s\S]*?\]);document\.querySelectorAll/);
if (!docsMatch) throw new Error('未找到工作流文档数据');
const docs = JSON.parse(docsMatch[1]).filter(x => x.name !== '可视化思维导图.txt');
wrapper = wrapper.replace(docsMatch[0], `const DOCS=${JSON.stringify(docs)};document.querySelectorAll`);
fs.writeFileSync(wrapperPath, wrapper, 'utf8');

for (const name of ['可视化思维导图.txt', '追夫火葬场引擎v2.0进化版_ima脑图.jpeg']) {
  const file = path.join(moduleDir, name);
  if (fs.existsSync(file)) fs.unlinkSync(file);
}

let app = fs.readFileSync(appPath, 'utf8');
app = app.replace(/(id="ntr-workflow-nav"[\s\S]*?追夫·绿帽工作流<span class="count">)7(<\/span>)/, '$15$2');
fs.writeFileSync(appPath, app, 'utf8');

console.log(JSON.stringify({ tabs: 5, documents: docs.map(x => x.label), removedFiles: 2 }, null, 2));
