import {encryptionConfig} from "../config/EncryptionConfig";

const sodium = require('sodium-native');

class EncryptionService{
    generateNonce(): Buffer{
        const nonce = Math.random() * (1 << 30) | 0;
        const nonceBuffer = Buffer.alloc(8);
        nonceBuffer.writeUInt32LE(nonce, 0);
        return nonceBuffer;
    }

    generateKeyPairs(): Buffer[]{
        const privateKey = Buffer.from(encryptionConfig.ChaChaSigPrivateKey, "base64");
        const publicKey = Buffer.from(encryptionConfig.ChaChaSigPublicKey, "base64");
        return [privateKey,publicKey];
    }

    signMessage(message: Buffer): Buffer{
        const [privateKey, publicKey] = this.generateKeyPairs();
        const signedMessage = Buffer.alloc(sodium.crypto_sign_BYTES + message.length);
        sodium.crypto_sign(signedMessage, message, privateKey);
        return signedMessage;
    }

    signOpenMessage(signedMessage: Buffer): Buffer{
        const [privateKey ,publicKey] = this.generateKeyPairs();
        const message = Buffer.alloc(signedMessage.length - sodium.crypto_sign_BYTES);
        sodium.crypto_sign_open(message, signedMessage, publicKey);
        return message;
    }

    encrypt(message: Buffer, nonce: number): Buffer{
        const key = Buffer.from(encryptionConfig.ChaChaEncryptionKey,"base64");
        const cipherText = Buffer.alloc(message.length);
        const nonceBuffer = Buffer.alloc(sodium.crypto_stream_NONCEBYTES);
        nonceBuffer.writeUInt32LE(nonce, 0);
        sodium.crypto_stream_chacha20_xor(cipherText, message, nonceBuffer, key);
        return cipherText;
    }

    decrypt(encrypted:Buffer, nonce: number): Buffer {
        const key = Buffer.from(encryptionConfig.ChaChaEncryptionKey, "base64");
        const plainText = Buffer.alloc(encrypted.length);
        const nonceBuffer = Buffer.alloc(sodium.crypto_stream_NONCEBYTES);
        nonceBuffer.writeUInt32LE(nonce, 0);
        sodium.crypto_stream_chacha20_xor(plainText, encrypted, nonceBuffer, key);
        return plainText;
    }
}

export default EncryptionService;