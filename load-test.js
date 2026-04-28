import http from 'k6/http';
import { check, sleep } from 'k6';

// Конфігурація: 100 віртуальних користувачів протягом 10 секунд (Стрес-тест)
export let options = {
    stages: [
        { duration: '3s', target: 50 },  // Розганяємось до 50 VUs
        { duration: '5s', target: 100 }, // Тримаємо 100 VUs
        { duration: '2s', target: 0 },   // Зменшуємо до 0 VUs
    ],
};

// URL вашого локального сервера (відповідно до .NET)
const BASE_URL = 'http://localhost:5032';

// id активних виборів (ви можете підставити з бази після запуску Seeder'а)
const ELECTION_ID = 'e2b4de09-5d7d-451e-8e50-25a83e0ac6ff'; 

export default function () {
    // 1. Читання результатів (Навантаження на БД)
    let getResponse = http.get(`${BASE_URL}/api/elections/${ELECTION_ID}/results`);
    
    // 2. Голосування (Імітація конкурентного доступу - стрес-тест)
    // Генеруємо випадкові email-адреси, щоб запобігти помилці "already voted"
    let randomEmail = `user_${__VU}_${__ITER}_${Math.random().toString(36).substring(7)}@example.com`;
    
    let votePayload = JSON.stringify({
        voterEmail: randomEmail,
        votes: [
            {
                candidateId: 'df70eac0-621b-4f51-a96d-c5cf152ee4db', // Один із кандидатів
                rank: null
            }
        ]
    });
    
    let params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };
    
    let postResponse = http.post(`${BASE_URL}/api/elections/${ELECTION_ID}/vote`, votePayload, params);
    
    // Перевірка
    check(getResponse, {
        'GET results status is 200 or 400 (if active)': (r) => r.status === 200 || r.status === 400, // 400 якщо вибори не закриті
    });
    
    check(postResponse, {
        'POST vote status is 200 (Success)': (r) => r.status === 200,
    });
    
    sleep(0.1); // мікро затримка між ітераціями
}
