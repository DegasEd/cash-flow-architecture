import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const requestLossRate = new Rate('request_loss_rate');

export const options = {
    scenarios: {
        consolidation_query: {
            executor: 'constant-arrival-rate',

            // Challenge requirement: 50 requests per second.
            rate: 50,
            timeUnit: '1s',

            // Keep the peak load long enough to observe Kubernetes/HPA behavior.
            duration: '2m',

            preAllocatedVUs: 20,
            maxVUs: 100,
        },
    },

    thresholds: {
        // Challenge tolerance: no more than 5% request loss.
        request_loss_rate: ['rate<=0.05'],

        // Additional technical validation.
        http_req_failed: ['rate<=0.05'],
    },
};

export default function () {
    const response = http.get(
        'http://localhost:8088/consolidations/2026-09-30'
    );

    const success = check(response, {
        'status is 200': (r) => r.status === 200,
        'balance is 5000 cents': (r) => {
            try {
                return r.json('balanceInCents') === 5000;
            } catch {
                return false;
            }
        },
    });

    requestLossRate.add(!success);
}