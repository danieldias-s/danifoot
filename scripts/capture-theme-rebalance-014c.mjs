// Import an ORIGINAL screenshot captured with the supported browser/Computer Use tool.
// No browser automation or image alteration occurs here.
// Usage: node scripts/capture-theme-rebalance-014c.mjs source.jpg label
import {copyFileSync,existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
const [input,label]=process.argv.slice(2);
if(!input || !/^[a-z0-9-]+$/.test(label??''))throw new Error('Provide original screenshot and lowercase label.');
const root=resolve(import.meta.dirname,'..');
if(root.replaceAll('\\','/').toLowerCase()!=='d:/elifoot_98_014c')throw new Error('014C worktree only.');
const bytes=readFileSync(resolve(input)),png=bytes.subarray(0,8).equals(Buffer.from('89504e470d0a1a0a','hex'));
const jpg=bytes[0]===255&&bytes[1]===216;if(!png&&!jpg)throw new Error('PNG/JPEG screenshot required.');
const out=join(root,'docs/audit-014c-rebalance/browser-captures');mkdirSync(out,{recursive:true});
const destination=join(out,label+(png?'.png':'.jpg'));if(existsSync(destination))throw new Error('Evidence already exists; use a new label.');
copyFileSync(resolve(input),destination);
writeFileSync(destination+'.json',JSON.stringify({File:destination,Source:resolve(input),Sha256:createHash('sha256').update(bytes).digest('hex'),Method:'Original supported browser tool screenshot; imported without alteration'},null,2));
console.log(destination);
