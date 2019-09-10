// https://github.com/DefinitelyTyped/DefinitelyTyped/blob/bc9f7f2bc2746ab9d3fca27efc5b1b62388a21d0/types/libsodium-wrappers-sumo/index.d.ts
// https://medium.com/@terence410/anti-cheat-in-unity-encryption-replay-attack-proxy-517a79df839a
// https://medium.com/@terence410/first-story-e9eba575af2

declare module "sodium-native" {
    export const crypto_secretbox_NONCEBYTES: any;
    export const crypto_sign_PUBLICKEYBYTES: any;
    export const crypto_sign_SECRETKEYBYTES: any;
    export const crypto_sign_BYTES: any;

    export function crypto_stream_chacha20_xor(chiper: Buffer, input: Buffer, nonce: Buffer, ouput: Buffer): void;

    export function crypto_sign(signedMessage: Buffer, message: Buffer, privateKey: Buffer);

    export function crypto_sign_open(message: Buffer, signedMessage: Buffer, publicKey: Buffer);

    export function crypto_sign_keypair(publicKey: Buffer, privateKey: Buffer);

    export function randombytes_buf(publicKey: Buffer): void;

    export function crypto_sign_seed_keypair(publicKey: Buffer, privateKey: Buffer, seed: Buffer): any;

    export function crypto_stream_chacha20_xor(ciphertext: Buffer, message: Buffer, nonce: Buffer, key: Buffer): any;
}
