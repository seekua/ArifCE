#!/usr/bin/env node
const { spawnSync } = require('node:child_process');
const result = spawnSync(process.platform === 'win32' ? 'dotnet.exe' : 'dotnet', ['tool', 'run', 'arifce', ...process.argv.slice(2)], { stdio: 'inherit' });
if (result.error) { console.error('ArifCE requires the .NET SDK and local ArifCE dotnet tool.'); process.exit(1); }
process.exit(typeof result.status === 'number' ? result.status : 1);
