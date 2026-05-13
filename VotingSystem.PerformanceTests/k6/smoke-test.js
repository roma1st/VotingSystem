// Smoke test — 1 VU, ~30s. Перевіряє що базові ендпоінти живі.
// Запуск: k6 run k6/smoke-test.js

import { check, sleep } from 'k6';
import {
    createElection,
    addCandidate,
    openElection,
    vote,
    closeElection,
    getResults,
    getTurnout,
    getElection,
    getElections,
} from './helpers/api-client.js';
import { voterEmail } from './helpers/config.js';

export const options = {
    vus: 1,
    duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<300'],
        checks: ['rate>0.99'],
    },
};

export default function () {
    // 1. Створити вибори
    const createRes = createElection();
    check(createRes, { 'create election 201': (r) => r.status === 201 });
    if (createRes.status !== 201) return;
    const electionId = JSON.parse(createRes.body).id;

    // 2. Додати кандидата
    const candRes = addCandidate(electionId);
    check(candRes, { 'add candidate 200': (r) => r.status === 200 });
    if (candRes.status !== 200) return;
    const candidateId = JSON.parse(candRes.body).id;

    // 3. Відкрити вибори
    const openRes = openElection(electionId);
    check(openRes, { 'open election 204': (r) => r.status === 204 });

    // 4. Прочитати вибори (хіт кеша на повторі)
    check(getElection(electionId), { 'get election 200': (r) => r.status === 200 });
    check(getElection(electionId), { 'get election cached 200': (r) => r.status === 200 });

    // 5. Список з фільтром
    check(getElections(1), { 'list active 200': (r) => r.status === 200 });

    // 6. Проголосувати
    const voteRes = vote(electionId, voterEmail('smoke'), [{ candidateId, rank: null }]);
    check(voteRes, { 'vote 200': (r) => r.status === 200 });

    // 7. Закрити та прочитати результати
    check(closeElection(electionId), { 'close 204': (r) => r.status === 204 });
    check(getResults(electionId), { 'results 200': (r) => r.status === 200 });
    check(getTurnout(electionId), { 'turnout 200': (r) => r.status === 200 });

    sleep(1);
}
