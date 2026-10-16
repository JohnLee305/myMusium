import fs from 'node:fs';
import {normalizeRooms} from '../apps/curator-admin/rooms.mjs';
try{
 const file=process.argv[2];if(!file||fs.statSync(file).size>4*1024*1024)throw Error('MANIFEST_SIZE_INVALID');
 normalizeRooms(JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'')));
 console.log('TOUR_NAVIGATION_VALID');
}catch(error){console.error('TOUR_NAVIGATION_INVALID: '+error.message);process.exitCode=1;}
