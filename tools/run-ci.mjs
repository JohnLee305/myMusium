import {spawnSync} from 'node:child_process';
const tests=['test-admin-workspace.mjs','test-task-coordination.mjs','test-ci-policy.mjs','test-git-preflight.mjs','test-navigation-editor-dom.mjs','test-navigation.mjs','test-reactions.mjs','test-reaction-registration.mjs','test-themes.mjs','test-admin.mjs','test-layout.mjs','test-preview.mjs','test-visitor-admin.mjs','test-visitor-diversity-admin.mjs','test-control.mjs'];
for(const file of tests){
 console.log('\nCI_TEST: '+file);
 const r=spawnSync(process.execPath,['tools/'+file],{stdio:'inherit',windowsHide:true,timeout:180000});
 if(r.status!==0||r.error){console.error('CI_TEST_FAILED: '+file);process.exit(1);}
}
for(const file of ['test-decor-contract.ps1','test-decor-commands.ps1','test-decor-store.ps1','test-decor-placement.ps1','test-decor-interaction.ps1']){
 console.log('\nCI_TEST: '+file);
 const result=spawnSync('pwsh',['-NoProfile','-File','tools/'+file],{stdio:'inherit',windowsHide:true,timeout:180000});
 if(result.status!==0||result.error){console.error('CI_TEST_FAILED: '+file);process.exit(1);}
}
console.log('MUSEUM_NODE_CI_PASS: '+tests.length+' node suites + 5 decor C# suites');
