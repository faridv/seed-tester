const fs = require('fs');
const readline = require('readline');

const inputFile = './password_log.csv';
const outputFile = './password_log.filtered.csv';

const search = 'ERROR_15';
const search2 = 'EXCEPTION';

const reader = readline.createInterface({
  input: fs.createReadStream(inputFile),
  crlfDelay: Infinity,
});

const writer = fs.createWriteStream(outputFile);

reader.on('line', (line) => {
  if (!line.includes(search) && !line.includes(search2)) {
    writer.write(line + '\n');
  }
});

reader.on('close', () => {
  writer.end();
  console.log('Done');
});