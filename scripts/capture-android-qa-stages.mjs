import fs from 'node:fs/promises';
import path from 'node:path';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
const exec = promisify(execFile);
const root = path.resolve(import.meta.dirname, '..');
const evidence = path.join(root, 'docs/android-runtime-003');
const log = path.resolve(process.argv[2] ?? path.join(evidence, 'native-profile-continued.log'));
const serial = process.argv[3] ?? 'emulator-5554';
const adb = path.join(root, '.tools/android-sdk/platform-tools/adb.exe');
const directory = path.join(evidence, 'screenshots');
await fs.mkdir(directory, {recursive:true});
const seen = new Set();
let world;
const end = Date.now()+40*60*1000;
while (Date.now()<end) {
  let text='';
  try { text=await fs.readFile(log,'utf8'); } catch {}
  const room=text.match(/QA003_ROOM (\{[^\r\n]+\})/);
  if (room) world=JSON.parse(room[1]).worldId;
  for (const stage of text.matchAll(/QA003_STAGE ([a-zA-Z0-9-]+)/g)) {
    const name=stage[1];
    if(seen.has(name)) continue;
    seen.add(name);
    try {
      const remote='/data/local/tmp/elifoot-qa003-stage.png';
      await exec(adb,['-s',serial,'shell','-x','screencap','-p',remote],{timeout:60000});
      await exec(adb,['-s',serial,'pull',remote,path.join(directory,`${name}.png`)],{timeout:60000});
      if(world) {
        const response=await fetch(`http://127.0.0.1:5080/api/worlds/${world}/snapshot`,{signal:AbortSignal.timeout(10000)});
        if(!response.ok) throw Error(`snapshot HTTP ${response.status}`);
        await fs.writeFile(path.join(directory,`${name}.json`),JSON.stringify(await response.json(),null,2));
      }
      console.log(`Captured ${name}`);
    } catch (error) { console.log(`Capture failed ${name}: ${error.message}`); }
  }
  if (/QA003_COMPLETE|Some tests failed|All tests passed|Application failed to start/.test(text)) break;
  await new Promise(resolve=>setTimeout(resolve,1500));
}
