// OpenFootball clubs.txt: each ordinary line starts a new club; only | lines are aliases.
export function parseClubDirectory(text, country, countryName, sourceFile) {
const clubs=[];let current;for(const raw of text.split(/\r?\n/)){let line=raw.split('#')[0].trim();if(!line||line.startsWith('=')||line.startsWith('~'))continue;if(line.startsWith('|')){if(current)current.aliases.push(...line.split('|').map(x=>x.trim()).filter(Boolean));continue;}const clubName=line.split(',')[0].trim();if(!clubName||/women|femen|damen|ladies/i.test(clubName))continue;current={country,countryName,name:clubName,aliases:[clubName],sourceLine:raw,sourceFile,founding:line.match(/,\s*(18\d\d|19\d\d|20\d\d)(?:,|\s|$)/)?.[1]};clubs.push(current);}
return clubs;
}
