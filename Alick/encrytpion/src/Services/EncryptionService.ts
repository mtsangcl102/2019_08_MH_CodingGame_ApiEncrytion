import * as config from '../config/EncryptionConfig'
import * as sodium from 'sodium-native';
import * as md5 from 'md5';
import * as crypto from 'crypto';


export class EncryptionService {
    public nonce: number = 0;
    public timeout: number;
    public lastRequest: number;
    private sodium: any;
    private publicKey: any;
    private privateKey: any;

    constructor() {
        // this.nonce = Math.random() * 256 * 256 * 256 | 0;
        this.nonce = Math.random() * (1 << 30) | 0;
    }

    public generateNonce() {
        // let nonce = Buffer.alloc(sodium.crypto_secretbox_NONCEBYTES);
        // sodium.randombytes_buf(nonce);
        // return Buffer.from((Math.random() * 100000 + 256 * 256 * 256 | 0).toString());
        const nonceBuffer = Buffer.alloc(8);
        nonceBuffer.writeUInt32LE(this.nonce, 0);
        this.nonce += 1;
        return nonceBuffer;
    }

    public generateNonceWithSize(size) {
        return Buffer.alloc(size);
    }

    public generateKeyPairs() {
        this.publicKey = this.generateNonceWithSize(sodium.crypto_sign_PUBLICKEYBYTES);
        this.privateKey = this.generateNonceWithSize(sodium.crypto_sign_SECRETKEYBYTES);
        sodium.crypto_sign_keypair(this.privateKey, this.privateKey);
        return [this.publicKey, this.privateKey];
    }

    public signMessage(message: Buffer): Buffer {
        let signedMessage = Buffer.alloc(sodium.crypto_sign_BYTES + message.length);
        sodium.crypto_sign(signedMessage, message, Buffer.from(config.encryptionConfig.ChaChaSigPrivateKey, 'base64'));
        return signedMessage;
    }

    public signOpenMessage(signedMessage: Buffer) {
        let message = Buffer.alloc(signedMessage.length - sodium.crypto_sign_BYTES);
        sodium.crypto_sign_open(message, signedMessage, this.publicKey);
        return message;
    }

    public encrypt(message: Buffer, nonceBuffer: Buffer): Buffer {
        let ciphertext = Buffer.alloc(message.length);
        let keyBuffer = Buffer.from(config.encryptionConfig.ChaChaEncryptionKey, 'base64');
        sodium.crypto_stream_chacha20_xor(ciphertext, message, nonceBuffer, keyBuffer);

        return ciphertext;
    }

    public decrypt(message: Buffer, nonceBuffer: Buffer) {
        let ciphertext = Buffer.alloc(message.length);
        let keyBuffer = Buffer.from(config.encryptionConfig.ChaChaEncryptionKey, 'base64');
        sodium.crypto_stream_chacha20_xor(ciphertext, message, nonceBuffer, keyBuffer);

        return ciphertext;
    }

    public static getMd5(message: string): string {
        return md5(message);
    }

    public get currentTimestamp(): number {
        return +new Date() / 1000 | 0;
    }
}
