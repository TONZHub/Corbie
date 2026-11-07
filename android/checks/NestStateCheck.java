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
        require(state.carried == Shiny.BOTTLE_CAP, "first shiny is the favourite");
        require(!state.carry(Shiny.PURPLE_D20) && state.carried == Shiny.BOTTLE_CAP, "locked item cannot be carried");
        require(state.carry(null) && state.carried == null && state.shinies == 1, "stashing preserves the hoard");
        require(state.carry(Shiny.BOTTLE_CAP) && state.shinies == 1, "taking an owned item preserves the count");
        NestState oldSave = new NestState(25, 9, 0, false, .47f, .78f);
        require(oldSave.carried == null && Shiny.BOTTLE_CAP.copies(oldSave.shinies) == 2
                && Shiny.PURPLE_D20.copies(oldSave.shinies) == 1, "legacy total maps to the collection");
        require(Shiny.restore("PAPERCLIP") == Shiny.PAPERCLIP && Shiny.restore("UNKNOWN") == null, "saved item ID");
        state.apply(NestState.Action.REST);
        require(state.resting && state.mood().equals("Roosting"), "rest mood");
        state.apply(NestState.Action.WAKE);
        require(!state.resting && state.bond == 10, "waking does not earn bond");
        for(int i=0;i<12000;i++) { state.apply(NestState.Action.TREAT);state.apply(NestState.Action.SHINY); }
        require(state.bond == 100 && state.shinies == 9999 && state.treats == 9999, "saturated state");
        state.perch(-1,2);
        require(state.perchX == 0 && state.perchY == 1, "bounded perch");
        require(new NestState(state.bond,state.shinies,state.treats,state.resting,state.perchX,state.perchY).mood().equals("Quite fond"), "state round trip");
        System.out.println("PASS: care, wake/rest, hoard migration, carry and stash, and state bounds");
    }
    private static void require(boolean condition,String label){if(!condition)throw new AssertionError(label);}
}
