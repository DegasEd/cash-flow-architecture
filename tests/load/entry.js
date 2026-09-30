import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const requestLossRate = new Rate('request_loss_rate');

export const options = {
  scenarios: {
    entry_write: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },

  thresholds: {
    request_loss_rate: ['rate<=0.05'],
    http_req_failed: ['rate<=0.05'],
  },
};

export default function () {
  const payload = JSON.stringify({
    amountInCents: 100,
    type: 2,
    occurredAt: '2026-09-30',
  });

  const response = http.post(
    'http://localhost:8089/entries',
    payload,
    {
      headers: {
        'Content-Type': 'application/json',
      },
    }
  );

  const success = check(response, {
    'status is 201': (r) => r.status === 201,
  });

  requestLossRate.add(!success);
}
