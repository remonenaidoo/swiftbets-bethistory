# swiftbets-bethistory

The bet history read model for SwiftBets: every coupon a punter placed, how it settled and what has been paid, for the customer's bet history page and for back-office lookups.

- Projects `placement.coupon-placed.v1` and `.v2`, `settlement.coupon-settled.v1` and `payout.payout-completed.v1` into `history.coupons` (Postgres, `sb_history`). Every projection is an idempotent upsert guarded by version, so duplicated or out-of-order events (a settlement before its placement) still converge.
- A V2 coupon and its V1 twin project once; system bets and bankers show as bet type `system`.
- The read model can be rebuilt from the topics: roll back, migrate and reset the consumer groups.

| Endpoint | Who |
|---|---|
| `GET /me/coupons?limit=&open=` | the punter, own coupons, newest first; `open=true` for unsettled only |
| `GET /me/coupons/{couponId}` | the punter, own coupon only |
| `GET /admin/history/punters/{punterId}/coupons?open=` | operators |
| `GET /admin/history/integrity?staleHours=72&graceMinutes=30` | operators: coupons open too long, settlements whose placement never arrived |
| `GET /admin/history/coupons/{couponId}` | operators |

| Project | Purpose |
|---|---|
| `SwiftBets.History.Application` | The store port and the row |
| `SwiftBets.History.Infrastructure` | Postgres store (Dapper) and the Kafka projectors |
| `SwiftBets.History.Api` | Customer and back-office endpoints |
| `SwiftBets.History.Migrator` | `sb_history` migrations with rollbacks |

`History:RunProjector=false` runs the read side only.

## Run the tests

```bash
../swiftbets-platform/scripts/fetch-shared-packages.sh .
dotnet test
```

Tests start Postgres with Testcontainers.
