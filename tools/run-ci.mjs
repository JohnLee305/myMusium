import {spawnSync} from 'node:child_process';
const tests=['test-ci-policy.mjs','test-git-preflight.mjs','test-navigation-editor-dom.mjs','test-navigation.mjs','test-reactions.mjs','test-reaction-registration.mjs','test-themes.mjs','test-admin.mjs','test-layout.mjs','test-preview.mjs','test-visitor-admin.mjs','test-visitor-diversity-admin.mjs','test-control.mjs'];
for(const file of tests){
 console.log('\nCI_TEST: '+file);
 const r=spawnSync(process.execPath,['tools/'+file],{stdio:'inherit',windowsHide:true,timeout:180000});
 if(r.status!==0||r.error){console.error('CI_TEST_FAILED: '+file);process.exit(1);}
}
console.log('MUSEUM_NODE_CI_PASS: '+tests.length+' suites');
