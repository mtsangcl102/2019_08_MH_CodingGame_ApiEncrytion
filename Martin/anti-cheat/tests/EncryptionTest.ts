import {assert} from 'chai';
import * as crypto from 'crypto';
import {EncryptionService} from '../src/providers/encryption.service';

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
        const encrypted = encryptionService.encrypt(message, nonce);
        const output = encryptionService.decrypt(encrypted, nonce);
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
            const hashBuffer = crypto.createHash('md5').update(message).digest();
            const signedMessage = encryptionService.signMessage(hashBuffer);
            const openedMessage = encryptionService.signOpenMessage(signedMessage);
        }
        console.timeEnd('case2');
    });
});