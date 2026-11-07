package com.tonzhub.corbie;

final class NestStateCheck {
    public static void main(String[] args) {
        NestState state = new NestState(-50, -5, -6, true, Float.NaN, Float.POSITIVE_INFINITY);
        require(state.bond == 0 && state.shinies == 0 && state.treats == 0, "bad saved counts");
        require(state.perchX == .47f && state.perchY == .78f, "bad saved position");
        state.apply(NestState.Action.GREET);
        require(!state.resting && state.bond == 1, "greeting wakes the bird");
        state.apply(NestState.Action.SCRITCH);
        state.apply(NestState.Action.TREAT);
        state.apply(NestState.Action.SHINY);
        require(state.bond == 10 && state.shinies == 1 && state.treats == 1, "care increments");
        state.apply(NestState.Action.REST);
        require(state.resting && state.mood().equals("Roosting"), "rest mood");
        state.apply(NestState.Action.WAKE);
        require(!state.resting && state.bond == 10, "waking does not earn bond");
        for(int i=0;i<12000;i++) { state.apply(NestState.Action.TREAT);state.apply(NestState.Action.SHINY); }
        require(state.bond == 100 && state.shinies == 9999 && state.treats == 9999, "saturated state");
        state.perch(-1,2);
        require(state.perchX == 0 && state.perchY == 1, "bounded perch");
        require(new NestState(state.bond,state.shinies,state.treats,state.resting,state.perchX,state.perchY).mood().equals("Quite fond"), "state round trip");
        System.out.println("PASS: care, wake/rest, state bounds, and invalid saved values");
    }
    private static void require(boolean condition,String label){if(!condition)throw new AssertionError(label);}
}
