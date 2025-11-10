# Mini manual test kit for Experiments + Responses

What this gives you:
- A small Postman collection to: create an experiment, add member `hss70`, post PQ and ati-scale responses, and fetch them back.
- Standalone JSON bodies you can paste into Postman if you prefer.

## Files
- `AI4NG-Experiment-Mini-ManualTests.postman_collection.json` – import into Postman
- `bodies/create-experiment.json` – body for creating an experiment with PQ and ati-scale
- `bodies/add-member.json` – body for adding a member (role=participant)
- `bodies/pq-response.json` – sample PQ response body
- `bodies/ati-scale-response.json` – sample ATI response body

## Prereqs
- Use your existing `awsTest` Postman environment (already in repo) and run the Auth flow to populate `idToken`.
- Ensure `apiGateWayUrl` ends with a trailing slash (the supplied env does). Requests use `{{apiGateWayUrl}}/api/...`.
- The collection uses variables: `experimentId`, `sessionId` (default `session-1`), `userSub` (default `hss70`). Adjust as needed in the collection or environment.

## Steps
1. Import the collection file above into Postman.
2. Select the `awsTest` environment (or one with `idToken` and `apiGateWayUrl`).
3. Run request "1) Create Experiment" – this captures `experimentId` into the environment.
4. Run "2) Add Member hss70" – adds the user to the experiment.
5. Run "3) Create PQ Response" – captures `pqResponseId`.
6. Run "4) Create ATI Response" – captures `atiResponseId`.
7. Optionally run the remaining GETs to verify.

If you prefer to paste bodies manually, open the files under `bodies/` and paste into the request body JSON editor in Postman.
