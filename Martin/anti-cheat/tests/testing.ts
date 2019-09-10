import {ApiClient} from './ApiClient';
import {assert} from 'chai';
import {ErrorCode} from '../src/states/ErrorCode';
import {ApiError} from '../src/errors/ApiError';
import {EncryptionService} from '../src/providers/encryption.service';
import * as crypto from 'crypto';
import axios from 'axios';
import * as fs from 'fs';
import * as zlib from 'zlib';

// set all test to have timeout of 60 seconds
// jest.setTimeout(60 * 1000);

// const host = 'http://127.0.0.1:80/';
const host = 'https://nestjs-api-test.herokuapp.com/';
const requestTimeout = 20 * 1000;
const serverSimulateTimeout = 5000;
const shortRequestTimeout = 1000;
const deviceId = 'any-32-bytes-string-works-000-99';

// group testing
const runAllEncryption = false;
const runAllUserApi = true;
const runAllErrorCardApi = true;
const runAllCardApi = true;

async function assertLoginError(apiClient: ApiClient, errorCode: ErrorCode) {
    try {
        await apiClient.login();
        assert.isFalse(true);
    } catch (e) {
        console.log(e);
        assert.isTrue((e instanceof ApiError));
        assert.equal(e.errorCode, errorCode);
    }
}

async function assertListCardError(apiClient: ApiClient, errorCode: ErrorCode) {
    try {
        await apiClient.listCards();
        assert.isFalse(true);
    } catch (e) {
        assert.isTrue((e instanceof ApiError));
        assert.equal(e.errorCode, errorCode);
    }
}



describe('/users/login', () => {
    if (!runAllUserApi) {
        return;
    }

    it('/users/login, empty request, invalid request', async () => {
        const instance = axios.create({
            responseType: 'arraybuffer',
            baseURL: host,
        });

        const post = {};
        const query = {};
        const result = await instance.post('/users/login', post, {params: query});
        const data = JSON.parse(result.data.toString());
        assert.equal(data.errorCode, ErrorCode.InvalidRequest);
    });

    it('/users/login, success', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();
        assert.equal(apiClient.user.deviceId, deviceId);
        assert.isNumber(apiClient.user.userId);
    });

    it('/users/login, invalid version', async () => {
        const apiClient = new ApiClient(host, 'not 32 byte', requestTimeout);
        apiClient.version = '2.0';
        await assertLoginError(apiClient, ErrorCode.InvalidVersion);
    });

    it('/users/login, invalid deviceId', async () => {
        const apiClient = new ApiClient(host, 'not 32 byte', requestTimeout);
        await assertLoginError(apiClient, ErrorCode.InvalidDeviceId);
    });

    it('/users/login, invalid client payload', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        apiClient.localSendInvalidRequestData = true;
        await assertLoginError(apiClient, ErrorCode.FailedToDecryptClientPayload);
    });

    it('/users/login, invalid server payload', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        apiClient.remoteSendInvalidPayload = true;
        await assertLoginError(apiClient, ErrorCode.FailedToDecryptServerPayload);
    });

    it('/users/login, timeout', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        apiClient.remoteTimeout = true;
        apiClient.timeout = shortRequestTimeout;
        await assertLoginError(apiClient, ErrorCode.Timeout);
        await new Promise(resolve => setTimeout(resolve, serverSimulateTimeout));
    });
});

describe('/cards/*, error', () => {
    if (!runAllErrorCardApi) {
        return;
    }

    it('/cards/list, invalid token', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();

        // list cards
        apiClient.token = 'invalid token';
        await assertListCardError(apiClient, ErrorCode.InvalidToken);
    });

    it('/cards/list, invalid session', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();

        // list cards
        apiClient.user.session = 'invalid session';
        await assertListCardError(apiClient, ErrorCode.InvalidSession);
    });

    it('/cards/list, invalid nonce', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();

        // list cards
        apiClient.nonce -= 1;
        await assertListCardError(apiClient, ErrorCode.InvalidNonce);
    });

    it('/cards/list, invalid timestamp', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();

        const limit = 4000; // 3600 seconds is the limit
        apiClient.serverTimestamp = apiClient.currentTimestamp + limit;
        await assertListCardError(apiClient, ErrorCode.InvalidTimestamp);
    });

    it('/cards/list, invalid sign', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();

        apiClient.localSendInvalidSignBase64 = true;
        await assertListCardError(apiClient, ErrorCode.InvalidSign);
    });

    it('/cards/list, invalid card id', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();

        // card a card with invalid monsterId
        try {
            const card = await apiClient.createCard(-1);
            assert.isFalse(true);
        } catch (e) {
            assert.isTrue((e instanceof ApiError));
            assert.equal(e.errorCode, ErrorCode.InvalidMonsterId);
        }

        // update a card with invalid cardId
        try {
            const card = await apiClient.updateCard(1000, 1);
            assert.isFalse(true);
        } catch (e) {
            assert.isTrue((e instanceof ApiError));
            assert.equal(e.errorCode, ErrorCode.InvalidCardId);
        }
    });
});

describe('/cards/*, success', () => {
    if (!runAllCardApi) {
        return;
    }

    it('/cards/list, success', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();
        assert.equal(apiClient.user.deviceId, deviceId);

        // list cards
        await apiClient.listCards();

        // delete all cards
        for (const card of apiClient.cards) {
            const respondData = await apiClient.deleteCard(card.id);
            assert.equal(respondData.body.card.id, card.id);
        }
        assert.equal(apiClient.cards.length, 0);

        const newCard = 5;
        for (let i = 0; i < newCard; i++) {
            const monsterId = i + 1;
            const respondData = await apiClient.createCard(monsterId);
            assert.equal(apiClient.cards.length, i + 1);
            assert.equal(respondData.body.card.monsterId, monsterId);
        }

        // update all cards
        for (let i = 0; i < newCard; i++) {
            const monsterId = i + 1;
            const exp = i * 10;
            const respondData = await apiClient.updateCard(monsterId, exp);
            assert.equal(respondData.body.card.exp, exp);
        }
    });

    it('/cards/list, try timeout and lock', async () => {
        const apiClient = new ApiClient(host, deviceId, requestTimeout);
        await apiClient.login();
        assert.equal(apiClient.user.deviceId, deviceId);

        // create a card and time out
        const monsterId = 100;
        try {
            apiClient.remoteTimeout = true;
            apiClient.timeout = shortRequestTimeout;
            const card = await apiClient.createCard(monsterId);
            assert.isFalse(true);
        } catch (e) {
            assert.isTrue((e instanceof ApiError));
            assert.equal(e.errorCode, ErrorCode.Timeout);
        }

        // we wait for server to finish the event first
        apiClient.remoteTimeout = false;
        apiClient.timeout = requestTimeout;

        // it should show a locked error
        try {
            assert.isTrue(apiClient.canResend);
            const respond = await apiClient.resend();
            assert.isFalse(true);
        } catch (e) {
            assert.isTrue((e instanceof ApiError));
            assert.equal(e.errorCode, ErrorCode.Locked);
        }

        // wait for server to finish timeout and unlock
        await new Promise(resolve => setTimeout(resolve, serverSimulateTimeout));

        try {
            // sending the same nonce still making things work from cache
            assert.isTrue(apiClient.canResend);
            const respondData = await apiClient.resend();
            assert.isFalse(apiClient.canResend);
            assert.equal(respondData.body.card.monsterId, monsterId);
            assert.isTrue(respondData.isCache);
        } catch (e) {
            console.log(e);
            // shouldn't go into here
            assert.isFalse(true);
        }
    });
});