import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '3s', target: 20 },  // Розігрів
        { duration: '5s', target: 50 },  // Навантаження
        { duration: '2s', target: 0 },   // Охолодження
    ],
};

const BASE_URL = 'http://localhost:5032'; 

// 1. Setup: Запускається 1 раз перед тестами. Готує дані!
export function setup() {
    let params = { headers: { 'Content-Type': 'application/json' } };
    
    // Створюємо вибори
    let createRes = http.post(`${BASE_URL}/api/elections`, JSON.stringify({
        title: "Load Test Election",
        description: "Checking performance",
        startDate: new Date().toISOString(),
        endDate: new Date(new Date().getTime() + 86400000).toISOString(),
        type: 0 // SingleChoice
    }), params);
    
    if (createRes.status !== 200 && createRes.status !== 201) return null;
    let electionId = JSON.parse(createRes.body).id;

    // Додаємо кандидата
    let candRes = http.post(`${BASE_URL}/api/elections/${electionId}/candidates`, JSON.stringify({
        name: "Test Candidate",
        description: "Candidate for Load",
        party: "Tech Party"
    }), params);
    let candidateId = JSON.parse(candRes.body).id;

    // Відкриваємо вибори
    http.post(`${BASE_URL}/api/elections/${electionId}/open`, null, params);

    return { electionId: electionId, candidateId: candidateId }; // Передаємо ці ID віртуальним юзерам
}

// 2. Default: Запускається тисячі разів віртуальними користувачами
export default function (data) {
    if (!data) return; // Якщо setup впав, нічого не робимо

    let getResponse = http.get(`${BASE_URL}/api/elections/${data.electionId}/results`);

    let randomEmail = `user_${__VU}_${__ITER}_${Math.random().toString(36).substring(7)}@example.com`;
    
    let votePayload = JSON.stringify({
        voterEmail: randomEmail,
        votes: [
            { candidateId: data.candidateId, rank: null }
        ]
    });
    
    let params = { headers: { 'Content-Type': 'application/json' } };
    let postResponse = http.post(`${BASE_URL}/api/elections/${data.electionId}/vote`, votePayload, params);
    
    check(getResponse, {
        'GET results status is 200': (r) => r.status === 200,
    });
    
    check(postResponse, {
        'POST vote status is 200 (Success)': (r) => r.status === 200,
    });
    
    sleep(0.1); 
}
