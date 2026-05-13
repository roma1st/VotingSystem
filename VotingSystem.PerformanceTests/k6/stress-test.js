// Stress test — поступове збільшення до 100 VU, шукаємо точку зламу.
// Запуск: k6 run k6/stress-test.js

import { check, sleep } from 'k6';
import { vote, getResults, getElection, getTurnout } from './helpers/api-client.js';
import { voterEmail } from './helpers/config.js';
import { prepareActiveElection } from './helpers/api-client.js';

export const options = {
    stages: [
        { duration: '10s', target: 20 },
        { duration: '20s', target: 60 },
        { duration: '20s', target: 100 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        // На stress-режимі ослаблюємо пороги — головне не повний колапс.
        http_req_failed: ['rate<0.10'],
        http_req_duration: ['p(95)<1000', 'p(99)<2000'],
    },
};

export function setup() {
    return prepareActiveElection({ type: 0 });
}

export default function (data) {
    if (!data || !data.electionId) return;

    // Перевіряємо що читання залишається швидким під навантаженням завдяки кешу.
    check(getElection(data.electionId), {
        'get election 200': (r) => r.status === 200,
    });

    check(getTurnout(data.electionId), {
        'turnout 200': (r) => r.status === 200,
    });

    // Конкурентні голоси з різних VU/ітерацій.
    const voteRes = vote(data.electionId, voterEmail('stress'), [
        { candidateId: data.candidateId, rank: null },
    ]);
    check(voteRes, {
        'vote accepted or rejected gracefully': (r) =>
            r.status === 200 || r.status === 400,
    });

    sleep(0.1);
}
