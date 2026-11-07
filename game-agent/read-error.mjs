export class GameReadError extends Error {
  constructor(message) {super(message);this.name='GameReadError';this.code='GAME_READ';}
}
