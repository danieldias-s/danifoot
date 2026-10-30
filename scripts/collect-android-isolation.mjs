import {spawn} from 'node:child_process';
import fs from 'node:fs/promises';
import {createWriteStream} from 'node:fs';
import path from 'node:path';
const root=path.resolve(import.meta.dirname,'..');
const adb=path.join(root,'.tools/android-sdk/platform-tools/adb.exe');
const label=process.argv[2]??'A-auto';
if(!/^[a-zA-Z0-9_-]+$/.test(label)) throw Error('Invalid evidence label');
const serial=process.env.QA_ANDROID_SERIAL??'emulator-5554';
const directory=path.join(root,'docs/android-root-cause-004',label);
await fs.mkdir(directory,{recursive:true});
const results=[];
async function run(name,args,timeout=20000) {
  const started=Date.now();
  const stdout=createWriteStream(path.join(directory,`${name}.stdout.log`));
  const stderr=createWriteStream(path.join(directory,`${name}.stderr.log`));
  const child=spawn(adb,args,{windowsHide:true});
  child.stdout.pipe(stdout);child.stderr.pipe(stderr);
  let timedOut=false;
  const timer=setTimeout(()=>{timedOut=true;child.kill();},timeout);
  const exit=await new Promise(resolve=>{
    child.once('error',error=>resolve({error:error.message}));
    child.once('close',(code,signal)=>resolve({code,signal}));
  });
  clearTimeout(timer);
  await Promise.all([new Promise(resolve=>stdout.end(resolve)),new Promise(resolve=>stderr.end(resolve))]);
  const result={name,args,...exit,timedOut,milliseconds:Date.now()-started};
  results.push(result);
  await fs.writeFile(path.join(directory,'commands.json'),JSON.stringify(results,null,2));
  console.log(JSON.stringify(result));
  return fs.readFile(path.join(directory,`${name}.stdout.log`),'utf8');
}
if(process.argv.includes('--wait')) {
  const deadline=Date.now()+8*60*1000;
  let n=0;
  let completed=false;
  while(Date.now()<deadline) {
    const state=await run(`boot-${++n}`,['-s',serial,'exec-out','getprop','sys.boot_completed'],10000);
    if(state.trim()==='1') {completed=true;break;}
    await new Promise(resolve=>setTimeout(resolve,10000));
  }
  await fs.writeFile(path.join(directory,'boot-result.json'),JSON.stringify({completed,polls:n},null,2));
  console.log('BOOT_WAIT_RESULT '+JSON.stringify({completed,polls:n}));
}
if(process.argv.includes('--passive')) {
  console.log('PASSIVE_IDLE_START '+new Date().toISOString());
  await new Promise(resolve=>setTimeout(resolve,60000));
  console.log('PASSIVE_IDLE_END '+new Date().toISOString());
}
await run('devices',['devices','-l']);
await run('getprop-shell',['-s',serial,'shell','getprop']);
await run('getprop-exec',['-s',serial,'exec-out','getprop']);
await run('processes',['-s',serial,'exec-out','ps']);
await run('third-party-packages',['-s',serial,'exec-out','pm','list','packages','-3']);
await run('activity-service',['-s',serial,'exec-out','service','check','activity']);
await run('package-service',['-s',serial,'exec-out','service','check','package']);
if(process.argv.includes('--light')) {
  await run('dropbox-anr',['-s',serial,'exec-out','dumpsys','dropbox','--print','system_app_anr']);
  await run('logcat',['-s',serial,'logcat','-b','all','-d','-t','1500','-v','threadtime'],30000);
  await run('screenshot',['-s',serial,'exec-out','screencap','-p','/data/local/tmp/qa004.png']);
  await run('screenshot-pull',['-s',serial,'pull','/data/local/tmp/qa004.png',path.join(directory,'screen.png')]);
  process.exit(0);
}
await run('cpuinfo',['-s',serial,'exec-out','dumpsys','cpuinfo']);
await run('meminfo',['-s',serial,'exec-out','dumpsys','meminfo']);
await run('lastanr',['-s',serial,'exec-out','dumpsys','activity','lastanr']);
await run('activity-full',['-s',serial,'exec-out','dumpsys','activity'],30000);
await run('dropbox-anr',['-s',serial,'exec-out','dumpsys','dropbox','--print','system_app_anr']);
await run('dropbox-watchdog',['-s',serial,'exec-out','dumpsys','dropbox','--print','system_server_watchdog']);
await run('anr-traces',['-s',serial,'exec-out','cat','/data/anr/traces.txt']);
await run('logcat',['-s',serial,'logcat','-b','all','-d','-v','threadtime'],30000);
await run('screenshot',['-s',serial,'exec-out','screencap','-p','/data/local/tmp/qa004.png']);
await run('screenshot-pull',['-s',serial,'pull','/data/local/tmp/qa004.png',path.join(directory,'screen.png')]);
if(process.argv.includes('--bugreport')) {
  await run('bugreport',['-s',serial,'bugreport',path.join(directory,'bugreport.zip')],180000);
}
