import test from 'node:test';
import assert from 'node:assert/strict';
import {parseClubDirectory} from './openfootball-directory.mjs';
test('A club without comma starts its own alias group',()=>{
 const clubs=parseClubDirectory('= Spain\nRacing Santander, 1913\n| Santander\nReal Valladolid CF\n| Valladolid\n','ES','Espanha','synthetic.clubs.txt');
 assert.equal(clubs.length,2);assert.deepEqual(clubs[0].aliases,['Racing Santander','Santander']);assert.deepEqual(clubs[1].aliases,['Real Valladolid CF','Valladolid']);
});
test('Comments and headings do not become clubs or aliases',()=>{
 const clubs=parseClubDirectory('# comment\n= League\n~ archive\nTeam A, 1901 # note\n| A # note\n\nTeam B\n','BR','Brasil','synthetic.clubs.txt');
 assert.equal(clubs.length,2);assert.equal(clubs[0].founding,'1901');assert.deepEqual(clubs[0].aliases,['Team A','A']);assert.deepEqual(clubs[1].aliases,['Team B']);
});
