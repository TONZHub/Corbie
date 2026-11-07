# Optional PM2 solver

`bridge.py` is Corbie's adapter. To enable numerical forecasts, place your own copy of the upstream `pm2solver` Python package at `game-agent/solver/pm2solver/`. It is ignored by Git. Python 3 is required.

The bridge uses the upstream tables and planner. Its survival mode searches three periods with initial jobs and free rest; it is advice, not a guaranteed route. Import actual in-game stats, and refresh them each month. Forecasts never replace observations.

Use `solver` in Corbie-Play.cmd for commands. Without the optional package, Corbie can still use the strategy guide and model planner. The solver integration tests need the local upstream package.
