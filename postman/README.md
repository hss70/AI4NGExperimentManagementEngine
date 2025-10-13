# Postman Collections

This folder contains Postman artifacts for the Experiment Management project.

- AI4NG-QuestionEngine.postman_collection.json (Unified)
  - Questionnaires, Experiments (incl. sync), Responses (with `data` wrapper), and Member Management.
  - Uses collection-level Bearer token with `{{jwt_token}}` variable.
- postman-local-collection.json (legacy) — can be removed after you switch to the unified collection.
- postman-cloud-collection.json (legacy) — can be removed after you switch to the unified collection.
- awsTest.postman_environment.json — example environment file.

Recommended: import only `AI4NG-QuestionEngine.postman_collection.json` going forward.
