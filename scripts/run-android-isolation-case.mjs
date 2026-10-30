import {spawn} from 'node:child_process';
import fs from 'node:fs/promises';
import {createWriteStream} from 'node:fs';
import path from 'node:path';
const root=path.resolve(import.meta.dirname,'..');
const adb=path.join(root,'.tools/android-sdk/platform-tools/adb.exe');
const label=process.argv[2];
const kind=process.argv[3];
if(!/^[A-Za-z0-9_-]+$/.test(label??'')) throw Error('Expected label');
if(!['native','reference','elifoot','connected'].includes(kind)) throw Error('Expected native/reference/elifoot/connected');
const serial=process.env.QA_ANDROID_SERIAL??'emulator-5554';
const dir=path.join(root,'docs/android-root-cause-004',label);
await fs.mkdir(dir,{recursive:true});
const results=[];
async function command(name,args,timeout=30000) {
  const started=Date.now();
  const out=createWriteStream(path.join(dir,`${name}.stdout.log`));
  const err=createWriteStream(path.join(dir,`${name}.stderr.log`));
  const p=spawn(adb,['-s',serial,...args],{windowsHide:true});
  p.stdout.pipe(out);p.stderr.pipe(err);
  let timedOut=false;
  const timer=setTimeout(()=>{timedOut=true;p.kill();},timeout);
  const result=await new Promise(resolve=>{
    p.once('error',e=>resolve({error:e.message}));
    p.once('close',(code,signal)=>resolve({code,signal}));
  });
  clearTimeout(timer);
  await Promise.all([new Promise(r=>out.end(r)),new Promise(r=>err.end(r))]);
  const stdout=await fs.readFile(path.join(dir,`${name}.stdout.log`),'utf8');
  const stderr=await fs.readFile(path.join(dir,`${name}.stderr.log`),'utf8');
  const controlCommand=/^(boot-check|install|launch|native-settings-launch|stop-)/.test(name);
  const remoteError=controlCommand&&/Error:|Failure\[|Error type \d|Can't connect|Could not access|Unknown command|Bad activity command|SERVICE DUMP TIMEOUT/i.test(stdout+stderr);
  const step={name,args,startedAt:new Date(started).toISOString(),milliseconds:Date.now()-started,...result,timedOut,
    accepted:!timedOut&&result.code===0&&!remoteError};
  results.push(step);
  await fs.writeFile(path.join(dir,'commands.json'),JSON.stringify(results,null,2));
  console.log(JSON.stringify(step));
  return {stdout,ok:step.accepted};
}
const boot=await command('boot-check',['exec-out','getprop','sys.boot_completed'],10000);
if(!boot.ok||boot.stdout.trim()!=='1') {
  const blocked={classification:'ENVIRONMENT_FAILURE',reason:'Android boot not confirmed; application experiment not started'};
  await fs.writeFile(path.join(dir,'blocked.json'),JSON.stringify(blocked,null,2));
  console.log(JSON.stringify(blocked));
  process.exit(2);
}
await command('pre-processes',['exec-out','ps']);
if(kind==='elifoot'||kind==='connected') {
  await command('stop-reference',['exec-out','am','force-stop','dev.elifoot.qa.android_reference']);
  await command('stop-settings',['exec-out','am','force-stop','com.android.settings']);
}
if(kind==='native') {
  await command('native-settings-launch',['exec-out','am','start','-W','-a','android.settings.SETTINGS'],60000);
} else {
  const reference=kind==='reference';
  const file=process.env.QA_ANDROID_APK??(reference?'tools/android_reference/build/app/outputs/flutter-apk/app-profile.apk'
    :kind==='connected'?'artifacts/android/elifoot-qa003-integration-profile.apk':'artifacts/android/elifoot-qa003-profile.apk');
  const remote='/data/local/tmp/qa004-test.apk';
  const pushed=await command('push',['push',path.join(root,file),remote],60000);
  const installed=pushed.ok?await command('install',['exec-out','pm','install','-r',remote],150000):{ok:false};
  if(installed.ok) {
    await command('launch',['exec-out','am','start','-W','-n',reference
      ?'dev.elifoot.qa.android_reference/.MainActivity':'com.example.elifoot_flutter/.MainActivity',
      '--ez','enable-software-rendering','true','--ez','enable-impeller','false'],60000);
  }
}
await new Promise(resolve=>setTimeout(resolve,30000));
await command('post-processes',['exec-out','ps']);
await command('post-service',['exec-out','service','check','activity']);
await command('logcat',['logcat','-b','all','-d','-t','1500','-v','threadtime'],30000);
await command('lastanr',['exec-out','dumpsys','activity','lastanr']);
await command('activity-full',['exec-out','dumpsys','activity'],30000);
await command('dropbox-anr',['exec-out','dumpsys','dropbox','--print','system_app_anr']);
await command('anr-traces',['exec-out','cat','/data/anr/traces.txt']);
await command('screen',['exec-out','screencap','-p','/data/local/tmp/qa004-case.png']);
await command('screen-pull',['pull','/data/local/tmp/qa004-case.png',path.join(dir,'screen.png')]);
if(results.some(step=>/^(boot-check|install|launch|native-settings-launch|stop-)/.test(step.name)&&!step.accepted)) {
  console.log('APPLICATION_EXPERIMENT_INCOMPLETE: inspect commands.json and raw outputs');
  process.exitCode=2;
}
