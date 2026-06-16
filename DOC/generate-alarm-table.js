const fs = require('fs');
const path = require('path');

const mapping = JSON.parse(fs.readFileSync('E:/Projects/SoftWare/Doc/alarm-id-mapping.json', 'utf-8'));

const headers = [
    '序号',
    '报警代码(ErrorCode)',
    '报警ID(MessageID)',
    '分类(Category)',
    '中文报警信息(Message)',
    '英文报警信息(MessageEn/MessageIDHex)',
    '严重级别(Severity)',
    '排故指导(Solution)',
    '图片路径(ImagePath)',
    '所在文件'
];

function escapeCsv(s) {
    if (s == null) return '';
    const str = String(s).replace(/\r\n/g, '\n');
    if (str.includes(',') || str.includes('"') || str.includes('\n')) {
        return '"' + str.replace(/"/g, '""') + '"';
    }
    return str;
}

function severityToNumber(severity) {
    const s = severity.replace('AlarmSeverity.', '');
    switch (s) {
        case 'Information': return 0;
        case 'Warning': return 1;
        case 'Error': return 2;
        case 'Fatal': return 3;
        default: return s;
    }
}

const rows = mapping.map((a, index) => [
    index + 1,
    a.errorCode,
    a.messageID,
    a.category,
    a.message,
    a.messageEn,
    severityToNumber(a.severity),
    a.solution,
    a.imagePath || '',
    a.file.replace('E:/Projects/SoftWare/', '')
].map(escapeCsv));

const csv = [headers.map(escapeCsv).join(','), ...rows.map(r => r.join(','))].join('\r\n');

fs.writeFileSync('E:/Projects/SoftWare/Doc/alarm-info-table.csv', csv, 'utf-8');
console.log(`Generated alarm-info-table.csv with ${mapping.length} alarms`);

// Also generate a markdown table (first 50 rows for preview)
function escapeMd(s) {
    return String(s).replace(/\|/g, '\\|').replace(/\n/g, '\\n').replace(/\r/g, '');
}

const mdHeaders = ['序号', '报警代码', '报警ID', '分类', '中文报警信息', '英文报警信息', '严重级别'];
const mdRows = mapping.map((a, i) => [
    i + 1,
    a.errorCode,
    a.messageID,
    a.category,
    a.message,
    a.messageEn,
    severityToNumber(a.severity)
].map(escapeMd));

const md = [
    '# 报警信息表',
    '',
    mdHeaders.join(' | '),
    mdHeaders.map(() => '---').join(' | '),
    ...mdRows.map(r => r.join(' | '))
].join('\n');

fs.writeFileSync('E:/Projects/SoftWare/Doc/alarm-info-table.md', md, 'utf-8');
console.log(`Generated alarm-info-table.md with ${mapping.length} alarms`);
