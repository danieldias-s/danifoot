import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../artifacts/015b/web');
const hash=createHash('sha256').update(fs.readFileSync(path.join(root,'main.dart.js'))).digest('hex');
const mime={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.png':'image/png','.jpg':'image/jpeg','.ico':'image/x-icon','.svg':'image/svg+xml','.ttf':'font/ttf','.woff':'font/woff','.woff2':'font/woff2'};
http.createServer((req,res)=>{
  const url=new URL(req.url,'http://127.0.0.1:5168');
  res.setHeader('Cache-Control','no-store');
  if(url.pathname==='/__015b_health'){res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify({app:'ELIFOOT_015B',api:'http://127.0.0.1:5167',web:'http://127.0.0.1:5168',root,sha256:hash}));return;}
  let name;try{name=decodeURIComponent(url.pathname);}catch{res.writeHead(400);res.end();return;}
  let file=path.resolve(root,'.'+name);
  if(file!==root&&!file.startsWith(root+path.sep)){res.writeHead(403);res.end();return;}
  if(file===root||name.endsWith('/'))file=path.join(file,'index.html');
  if(!fs.existsSync(file)||!fs.statSync(file).isFile()){
    if(!path.extname(name))file=path.join(root,'index.html');
    else{res.writeHead(404);res.end();return;}
  }
  res.writeHead(200,{'Content-Type':mime[path.extname(file)]||'application/octet-stream'});
  fs.createReadStream(file).pipe(res);
}).listen(5168,'127.0.0.1',()=>process.stdout.write('ELIFOOT_015B http://127.0.0.1:5168 | API 5167 | '+hash+'\n'));
