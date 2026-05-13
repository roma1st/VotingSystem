// Спільні налаштування для всіх k6 сценаріїв.
// BASE_URL можна перевизначити через env: -e BASE_URL=http://localhost:5032

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5032';

export const HEADERS = {
    headers: { 'Content-Type': 'application/json' },
};

export const ENDPOINTS = {
    elections: `${BASE_URL}/api/elections`,
    electionById: (id) => `${BASE_URL}/api/elections/${id}`,
    candidates: (id) => `${BASE_URL}/api/elections/${id}/candidates`,
    open: (id) => `${BASE_URL}/api/elections/${id}/open`,
    close: (id) => `${BASE_URL}/api/elections/${id}/close`,
    vote: (id) => `${BASE_URL}/api/elections/${id}/vote`,
    results: (id) => `${BASE_URL}/api/elections/${id}/results`,
    turnout: (id) => `${BASE_URL}/api/elections/${id}/turnout`,
};

// Спільні пороги — кожен сценарій може додавати або перевизначати.
export const DEFAULT_THRESHOLDS = {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
};

// Унікальний voter email на кожну ітерацію віртуального юзера.
export function voterEmail(prefix = 'voter') {
    return `${prefix}_${__VU}_${__ITER}_${Math.random().toString(36).slice(2, 8)}@example.com`;
}

export function nowIso(offsetDays = 0) {
    const d = new Date();
    d.setUTCDate(d.getUTCDate() + offsetDays);
    return d.toISOString();
}
