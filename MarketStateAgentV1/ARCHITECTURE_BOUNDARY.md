# Architecture boundary

Market State observes and classifies. It does not decide portfolio admission, grant risk authority, or execute broker actions.

The new contraction/expansion dimension is therefore published as read-only context. Existing execution-intent behavior remains unchanged in V1.2.0. A future strategy, portfolio orchestrator, or risk policy may consume the state through an explicit contract, but such coupling requires a separate reviewed change.
