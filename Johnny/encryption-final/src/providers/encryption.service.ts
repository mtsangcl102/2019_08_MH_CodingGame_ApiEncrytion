import * as key from '../../EncryptionConfig';
import * as sodium from 'sodium-native';
import {sign} from "crypto";

export class EncryptionService{
    private publicKey;
    private privateKey;
    private encryptionKey;
    private nonce;

    constructor(){
        [this.publicKey, this.privateKey] = this.generateKeyPairs();
        this.encryptionKey = Buffer.from(key.encryptionConfig.ChaChaEncryptionKey, 'base64');
    }

    public generateNonce(nonce: number){
        this.nonce = nonce;
        let nonceBuffer = Buffer.alloc(8);
        nonceBuffer.writeUInt32LE(this.nonce, 0);
        this.nonce++;
        return nonceBuffer;
    }

    public getEncryptionKey(){
        return this.encryptionKey;
    }

    public generateKeyPairs(){
        return [Buffer.from(key.encryptionConfig.ChaChaSigPublicKey, 'base64'), Buffer.from(key.encryptionConfig.ChaChaSigPrivateKey, 'base64')];
    }

    public signMessage(message: Buffer){
        let signedMessage = Buffer.alloc(sodium.crypto_sign_BYTES + message.length);
        sodium.crypto_sign(signedMessage, message, this.privateKey);

        return signedMessage;
    }

    public encrypt(message: Buffer, nonce: Buffer){
        let encryptedMessage = Buffer.alloc(message.length);

        sodium.crypto_stream_chacha20_xor(encryptedMessage, message, nonce, this.encryptionKey);

        return encryptedMessage;
    }

    public decrypt(message: Buffer, nonce: Buffer){
        let decryptedMessage = Buffer.alloc(message.length);
        sodium.crypto_stream_chacha20_xor(decryptedMessage, message, nonce, this.encryptionKey);

        return decryptedMessage;
    }

    public signOpenMessage(signedMessage: Buffer){
        let message = Buffer.alloc(signedMessage.length - sodium.crypto_sign_BYTES)
        sodium.crypto_sign_open(message, signedMessage, this.publicKey)

        return message;
    }
}