import * as sodium from 'sodium-native';
import {encryptionConfig} from '../EncryptionConfig';

export class EncryptionService {
    constructor() {}

    generateNonce() {
        return Math.random() * (1 << 30) | 0;
    }

    generateKeyPairs() {
        let publicKey = Buffer.from(encryptionConfig.ChaChaSigPublicKey, "base64");
        let privateKey = Buffer.from(encryptionConfig.ChaChaSigPrivateKey, "base64");
        sodium.crypto_sign_keypair(publicKey, privateKey);
        return [publicKey, privateKey];
    }

    encrypt(message, nonceBuffer) {
        // let nonceBuffer = Buffer.alloc(8);
        // nonceBuffer.writeUInt32LE(nonce, 0);
        let ciphertext = Buffer.alloc(message.length);
        let encryptionKey = Buffer.from(encryptionConfig.ChaChaEncryptionKey, "base64");
        sodium.crypto_stream_chacha20_xor(ciphertext, message, nonceBuffer, encryptionKey);
        return ciphertext;
    }

    decrypt(encrypted, nonceBuffer) {
        // let nonceBuffer = Buffer.alloc(8);
        // nonceBuffer.writeUInt32LE(nonce, 0);
        let ciphertext = Buffer.alloc(encrypted.length);
        let encryptionKey = Buffer.from(encryptionConfig.ChaChaEncryptionKey, "base64");
        sodium.crypto_stream_chacha20_xor(ciphertext, encrypted, nonceBuffer, encryptionKey);
        return ciphertext;
    }

    signMessage(message) {
        let signedMessage = Buffer.alloc(sodium.crypto_sign_BYTES + message.length);
        sodium.crypto_sign(signedMessage, message, Buffer.from(encryptionConfig.ChaChaSigPrivateKey, "base64"));
        return signedMessage;
    }

    signOpenMessage(signedMessage) {
        let message = Buffer.alloc(signedMessage.length - sodium.crypto_sign_BYTES);
        sodium.crypto_sign_open(message, signedMessage, Buffer.from(encryptionConfig.ChaChaSigPublicKey, "base64"));
        return message;
    }
}