const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
  const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
  const file = path.resolve(__dirname, '../output/beihaiZ/北海.html').replace(/\\/g, '/');
  await page.goto(`file:///${encodeURI(file)}`);
  await page.click('#ntr-workflow-nav');
  await page.waitForSelector('#ntr-workflow-view:not([hidden])');
  const frameHandle = await page.waitForSelector('#ntr-workflow-frame');
  const workflow = await frameHandle.contentFrame();
  await workflow.waitForSelector('#brain-frame');
  const brainHandle = await workflow.waitForSelector('#brain-frame');
  const brain = await brainHandle.contentFrame();
  await brain.waitForSelector('body');
  const brainTitle = await brain.title();

  await workflow.click('[data-panel="doc0"]');
  const docLength = await workflow.locator('#doc0 .content').evaluate(el => el.textContent.length);
  await page.screenshot({ path: path.resolve(__dirname, 'qa-ntr-workflow.png') });

  await page.click('#back-ntr-library');
  await page.click('#generator-nav');
  await page.waitForSelector('#generator-view:not([hidden])');
  const result = {
    brainTitle,
    workflowTabs: await workflow.locator('.tab').count(),
    docLength,
    removedTabsAbsent: await workflow.locator('text=思维导图文本').count() === 0 && await workflow.locator('text=引擎脑图').count() === 0,
    generatorStillWorks: await page.locator('#generator-view').isVisible()
  };
  if (!brainTitle.includes('NTR绿帽题材炸裂脑洞库') || result.workflowTabs !== 5 || docLength < 1000 || !result.removedTabsAbsent || !result.generatorStillWorks) {
    throw new Error(JSON.stringify(result));
  }
  console.log(JSON.stringify(result, null, 2));
  await browser.close();
})().catch(error => { console.error(error); process.exitCode = 1; });
