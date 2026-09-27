const fs = require("fs");

const FILE = "password_log.csv";

const KEEP_LINES = 119_687_861;
const BUFFER_SIZE = 16 * 1024 * 1024;

const fd = fs.openSync(FILE, "rs+");

const buffer = Buffer.allocUnsafe(BUFFER_SIZE);

let position = 0;
let lines = 0;

while (true) {
    const bytesRead = fs.readSync(fd, buffer, 0, BUFFER_SIZE, position);

    if (bytesRead === 0)
        throw new Error("File ended before target line.");

    for (let i = 0; i < bytesRead; i++) {
        if (buffer[i] === 10) {
            lines++;

            if (lines === KEEP_LINES) {
                const truncateAt = position + i + 1;

                fs.ftruncateSync(fd, truncateAt);
                fs.closeSync(fd);

                console.log(`Truncated at byte ${truncateAt.toLocaleString()}`);
                process.exit(0);
            }
        }
    }

    position += bytesRead;
}