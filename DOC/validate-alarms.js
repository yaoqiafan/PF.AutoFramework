const fs = require('fs');

const files = [
    'E:/Projects/SoftWare/PF.Core/Constants/AlarmCodes.cs',
    'E:/Projects/SoftWare/PF.WorkStation.AutoOcr/CostParam/AlarmCodesExtensions.cs'
];

function parseArguments(block) {
    const normalized = block.replace(/\n\s*/g, ' ');
    const args = [];
    let current = '';
    let depth = 0;
    let inString = false;
    let stringChar = '';

    for (let i = 0; i < normalized.length; i++) {
        const ch = normalized[i];
        const prev = normalized[i - 1];

        if (inString) {
            current += ch;
            if (ch === stringChar && prev !== '\\') {
                inString = false;
            }
        } else {
            if (ch === '"' || ch === "'") {
                inString = true;
                stringChar = ch;
                current += ch;
            } else if (ch === '(' || ch === '[' || ch === '{') {
                depth++;
                current += ch;
            } else if (ch === ')' || ch === ']' || ch === '}') {
                depth--;
                current += ch;
            } else if (ch === ',' && depth === 0) {
                args.push(current.trim());
                current = '';
            } else {
                current += ch;
            }
        }
    }
    if (current.trim()) args.push(current.trim());

    return args.map(arg => {
        if (arg.includes('+')) {
            const parts = arg.split('+').map(p => stripQuotes(p.trim()));
            return parts.join('');
        }
        return stripQuotes(arg);
    });
}

function stripQuotes(s) {
    if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
        return s.slice(1, -1);
    }
    return s;
}

function parseFile(filePath) {
    const content = fs.readFileSync(filePath, 'utf-8');
    const text = content.replace(/\r\n/g, '\n');

    const alarms = [];
    const attrRegex = /\[AlarmInfo\((.*?)\)\]/gs;
    let match;

    while ((match = attrRegex.exec(text)) !== null) {
        const attrBlock = match[1];
        const startIndex = match.index;
        const afterAttr = text.substring(startIndex + match[0].length);
        const constMatch = afterAttr.match(/^\s*public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;/m);
        if (!constMatch) continue;

        const args = parseArguments(attrBlock);
        alarms.push({
            file: filePath,
            constName: constMatch[1],
            errorCode: constMatch[2],
            category: args[0] || '',
            message: args[1] || '',
            messageEn: args[2] || '',
            severity: args[3] || '',
            solution: args[4] || '',
            messageID: args[5] || '',
            messageIDHex: args[6] || '',
            imagePath: args[7] || null,
            argCount: args.length
        });
    }

    return alarms;
}

const allAlarms = [];
for (const file of files) {
    allAlarms.push(...parseFile(file));
}

let errors = 0;
const idSet = new Set();
const codeSet = new Set();

console.log(`Scanned ${allAlarms.length} alarms\n`);

for (const a of allAlarms) {
    const loc = `${a.file}:${a.constName} (${a.errorCode})`;

    // Check argument count
    if (a.argCount !== 7 && a.argCount !== 8) {
        console.error(`[ERR] ${loc}: expected 7 or 8 args, got ${a.argCount}`);
        errors++;
    }

    // Check messageID
    const id = parseInt(a.messageID, 10);
    if (isNaN(id)) {
        console.error(`[ERR] ${loc}: MessageID is not a number: ${a.messageID}`);
        errors++;
    } else {
        if (id < 10000) {
            console.error(`[ERR] ${loc}: MessageID ${id} < 10000`);
            errors++;
        }
        if (idSet.has(id)) {
            console.error(`[ERR] ${loc}: duplicate MessageID ${id}`);
            errors++;
        }
        idSet.add(id);
    }

    // Check error code uniqueness
    if (codeSet.has(a.errorCode)) {
        console.error(`[ERR] ${loc}: duplicate ErrorCode ${a.errorCode}`);
        errors++;
    }
    codeSet.add(a.errorCode);

    // Check messageEn
    if (!a.messageEn) {
        console.error(`[ERR] ${loc}: MessageEn is empty`);
        errors++;
    } else if (a.messageEn.length > 35) {
        console.error(`[ERR] ${loc}: MessageEn length ${a.messageEn.length} > 35: "${a.messageEn}"`);
        errors++;
    }

    // Check messageIDHex
    if (!a.messageIDHex) {
        console.error(`[ERR] ${loc}: MessageIDHex is empty`);
        errors++;
    } else {
        if (a.messageIDHex.length > 35) {
            console.error(`[ERR] ${loc}: MessageIDHex length ${a.messageIDHex.length} > 35: "${a.messageIDHex}"`);
            errors++;
        }
        if (a.messageIDHex.length > 40) {
            console.error(`[ERR] ${loc}: MessageIDHex length ${a.messageIDHex.length} > 40: "${a.messageIDHex}"`);
            errors++;
        }
    }

    // Check messageEn == messageIDHex
    if (a.messageEn && a.messageIDHex && a.messageEn !== a.messageIDHex) {
        console.error(`[ERR] ${loc}: MessageEn and MessageIDHex differ`);
        errors++;
    }
}

console.log(`\n${errors === 0 ? 'Validation PASSED' : `Validation FAILED with ${errors} errors`}`);
process.exit(errors > 0 ? 1 : 0);
