import * as encryptionConfig from './EncryptionConfig';
import * as sodium from 'sodium-native';

export class EncryptionService {

    public nonce: number;

    constructor(){
        this.nonce = Math.random() * (1 << 30) | 0;
    }

    public generateNonce(nonce) {
        const nonceBuffer = Buffer.alloc(8);
        nonceBuffer.writeUInt32LE(nonce, 0);
        return nonceBuffer;
    }

    public generateKeyPairs() {
        let pk = Buffer.alloc(sodium.crypto_sign_PUBLICKEYBYTES);
        let sk = Buffer.alloc(sodium.crypto_sign_SECRETKEYBYTES);
        sodium.crypto_sign_keypair(pk, sk);
        return [pk, sk];
    }

    public signMessage(message, setInvalid = false){
        let signedMessage = Buffer.alloc(sodium.crypto_sign_BYTES + message.length);
        let privatekey = setInvalid ? Buffer.alloc(sodium.crypto_sign_BYTES) : Buffer.from(encryptionConfig.encryptionConfig.ChaChaSigPrivateKey, 'base64');
        sodium.crypto_sign(signedMessage, message, privatekey);
        return signedMessage;
    }

    public signOpenMessage(signedMessage){
        let message  = Buffer.alloc(signedMessage.length - sodium.crypto_sign_BYTES);
        sodium.crypto_sign_open(message, signedMessage, Buffer.from(encryptionConfig.encryptionConfig.ChaChaSigPublicKey,'base64'));
        return signedMessage;
    }

    public encrypt(message, nonce, setInvalid = false){
        let ciphertext = Buffer.alloc(message.length);
        nonce = Buffer.alloc(sodium.crypto_stream_NONCEBYTES, nonce);
        let encryptKeyBuffer = setInvalid ? Buffer.alloc(sodium.crypto_stream_KEYBYTES) : Buffer.from(encryptionConfig.encryptionConfig.ChaChaEncryptionKey,'base64');
        sodium.crypto_stream_chacha20_xor(ciphertext, message, nonce, encryptKeyBuffer);
        return ciphertext;
    }

    public decrypt(encrypted, nonce){
        let ciphertext = Buffer.alloc(encrypted.length);
        nonce = Buffer.alloc(sodium.crypto_stream_NONCEBYTES, nonce);
        sodium.crypto_stream_chacha20_xor(ciphertext, encrypted, nonce, Buffer.from(encryptionConfig.encryptionConfig.ChaChaEncryptionKey,'base64'));
        return ciphertext;
    }

}