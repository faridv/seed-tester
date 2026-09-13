const fs = require('fs');
const readline = require('readline');

const filePath = 'EEEEEEEE_FFFFFFFFF.csv';
const searchString = '6E07 646C B21F 77EA';

async function searchCSV(filePath, searchString) {
    const stream = fs.createReadStream(filePath, {
        encoding: 'utf8'
    });

    const rl = readline.createInterface({
        input: stream,
        crlfDelay: Infinity
    });

    let lineNumber = 0;

    for await (const line of rl) {
        lineNumber++;

        if (line.includes(searchString)) {
            console.log(`Found on line ${lineNumber}:`);
            console.log(line);

            rl.close();
            stream.destroy();
            return true;
        }
    }
	
	console.log('String not found.');
    return false;
}

searchCSV(filePath, searchString);