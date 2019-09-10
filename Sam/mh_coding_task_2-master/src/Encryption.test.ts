import EncryptionService from "./providers/encryption.service";
import * as assert from 'assert';
import * as crypto from 'crypto';

describe('encryption', () => {
    const encryptionService: EncryptionService = new EncryptionService();

    it('encryption generate', () => {
        const randomKey = encryptionService.generateNonce();
        const [publicKey, privateKey] = encryptionService.generateKeyPairs();
    });

    it('encryption basic test', () => {
        const input = {name: 'terence', company: 'madhead', address: 'sciencepark'};
        const message = Buffer.from(JSON.stringify(input));
        const nonce = 256 * 256 * 256 + 1;
        const signed = encryptionService.signMessage(message);
        console.log(message.toString('base64'));
        const encrypted = encryptionService.encrypt(message, nonce);
        const output = encryptionService.decrypt(encrypted, nonce);
        console.log(output.toString('base64'));
        assert.deepEqual(message, output);
        console.log('nonce', nonce);
        console.log('encrypted', encrypted.toString('base64'));
        console.log('signed', signed.toString('base64'));
    });

    // 1000 * 10 Loops: 5.2 seconds
    it('encryption performance test', () => {
        const loop = 1000; // should use 1000 * 10
        const message = Buffer.alloc(1000 * 100);

        console.time('case1');
        for (let i = 0; i < loop; i++) {
            const signedMessage = encryptionService.signMessage(message);
            const openedMessage = encryptionService.signOpenMessage(signedMessage);
            // console.log('case1', openedMessage.length);
        }
        console.timeEnd('case1');

        console.time('case2');
        for (let i = 0; i < loop; i++) {
            const encrypted = encryptionService.encrypt(message, i);
            const decrypted = encryptionService.decrypt(encrypted, i);
            assert.deepEqual(message, decrypted);
            const hashBuffer = crypto.createHash('md5').update(message).digest();
            const signedMessage = encryptionService.signMessage(hashBuffer);
            const openedMessage = encryptionService.signOpenMessage(signedMessage);
            assert.deepEqual(hashBuffer,openedMessage);
        }
        console.timeEnd('case2');
    });
});