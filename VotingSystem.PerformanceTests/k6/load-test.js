// Load test — нормальне навантаження.
// Запуск: k6 run k6/load-test.js

import { check, sleep } from 'k6';
import { vote, getResults, getElections, getElection } from './helpers/api-client.js';
import { voterEmail, DEFAULT_THRESHOLDS } from './helpers/config.js';
import { prepareActiveElection } from './helpers/api-client.js';

export const options = {
    stages: [
        { duration: '15s', target: 10 },
        { duration: '30s', target: 30 },
        { duration: '15s', target: 0 },
    ],
    thresholds: {
        ...DEFAULT_THRESHOLDS,
        checks: ['rate>0.95'],
    },
};

export function setup() {
    // Готуємо активні вибори з кандидатом, які потім обстріляють VU.
    return prepareActiveElection({ type: 0 });
}

export default function (data) {
    if (!data || !data.electionId) return;

    // Читання — найбільший вплив від кешу.
    check(getElection(data.electionId), {
        'get election 200': (r) => r.status === 200,
    });

    check(getElections(1), {
        'list active 200': (r) => r.status === 200,
    });

    // Запис — більшість будуть валідні; деякі повертатимуть 400 (дубль email).
    const voteRes = vote(data.electionId, voterEmail('load'), [
        { candidateId: data.candidateId, rank: null },
    ]);
    check(voteRes, {
        'vote 200 or duplicate 400': (r) => r.status === 200 || r.status === 400,
    });

    sleep(0.2);
}
