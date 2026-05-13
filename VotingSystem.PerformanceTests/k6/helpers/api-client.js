// Тонкі обгортки навколо HTTP виклику. Кожен сценарій імпортує лише потрібне.

import http from 'k6/http';
import { ENDPOINTS, HEADERS, nowIso } from './config.js';

export function createElection(overrides = {}) {
    const body = {
        title: overrides.title || 'k6 election',
        description: overrides.description || 'created from k6',
        startDate: overrides.startDate || nowIso(0),
        endDate: overrides.endDate || nowIso(1),
        type: overrides.type ?? 0, // SingleChoice
    };
    return http.post(ENDPOINTS.elections, JSON.stringify(body), HEADERS);
}

export function addCandidate(electionId, overrides = {}) {
    const body = {
        name: overrides.name || 'k6 candidate',
        description: overrides.description || 'auto-added',
        party: overrides.party || 'Tech',
        photoUrl: overrides.photoUrl ?? null,
    };
    return http.post(ENDPOINTS.candidates(electionId), JSON.stringify(body), HEADERS);
}

export function openElection(electionId) {
    return http.post(ENDPOINTS.open(electionId), null, HEADERS);
}

export function closeElection(electionId) {
    return http.patch(ENDPOINTS.close(electionId), null, HEADERS);
}

export function vote(electionId, voterEmailValue, votes) {
    const body = { voterEmail: voterEmailValue, votes };
    return http.post(ENDPOINTS.vote(electionId), JSON.stringify(body), HEADERS);
}

export function getElection(electionId) {
    return http.get(ENDPOINTS.electionById(electionId), HEADERS);
}

export function getElections(statusFilter) {
    const url = statusFilter !== undefined
        ? `${ENDPOINTS.elections}?status=${statusFilter}`
        : ENDPOINTS.elections;
    return http.get(url, HEADERS);
}

export function getResults(electionId) {
    return http.get(ENDPOINTS.results(electionId), HEADERS);
}

export function getTurnout(electionId) {
    return http.get(ENDPOINTS.turnout(electionId), HEADERS);
}

// Готує активні вибори з кандидатом — повертає { electionId, candidateId }.
// Кидає помилку якщо щось пішло не так — це переривання setup().
export function prepareActiveElection(opts = {}) {
    const createRes = createElection({ type: opts.type ?? 0 });
    if (createRes.status !== 201 && createRes.status !== 200) {
        throw new Error(`createElection failed: ${createRes.status} ${createRes.body}`);
    }
    const electionId = JSON.parse(createRes.body).id;

    const candRes = addCandidate(electionId, { name: 'Setup Candidate' });
    if (candRes.status !== 200 && candRes.status !== 201) {
        throw new Error(`addCandidate failed: ${candRes.status} ${candRes.body}`);
    }
    const candidateId = JSON.parse(candRes.body).id;

    const openRes = openElection(electionId);
    if (openRes.status !== 204 && openRes.status !== 200) {
        throw new Error(`openElection failed: ${openRes.status} ${openRes.body}`);
    }

    return { electionId, candidateId };
}
