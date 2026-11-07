package com.tonzhub.corbie;

/** Pocket Nest state stays small and independent of Android UI. */
final class NestState {
    enum Action { GREET, SCRITCH, TREAT, SHINY, REST, WAKE }
    int bond, shinies, treats;
    boolean resting;
    Shiny carried;
    float perchX, perchY;

    NestState(int bond, int shinies, int treats, boolean resting, float x, float y) {
        this.bond = clamp(bond, 0, 100);
        this.shinies = clamp(shinies, 0, 9999);
        this.treats = clamp(treats, 0, 9999);
        this.resting = resting;
        perch(x, y);
    }

    void apply(Action action) {
        switch (action) {
            case GREET: resting = false; bond = clamp(bond + 1, 0, 100); break;
            case SCRITCH: resting = false; bond = clamp(bond + 3, 0, 100); break;
            case TREAT: resting = false; treats = clamp(treats + 1, 0, 9999); bond = clamp(bond + 4, 0, 100); break;
            case SHINY:
                resting = false;
                if (shinies < 9999) carried = Shiny.next(shinies++);
                bond = clamp(bond + 2, 0, 100);
                break;
            case REST: resting = true; break;
            case WAKE: resting = false; break;
        }
    }

    String mood() {
        if (resting) return "Roosting";
        if (bond >= 65) return "Quite fond";
        if (bond >= 20) return "Warming up";
        return "Curious";
    }

    void updateCare(NestState saved) {
        bond = saved.bond;
        shinies = saved.shinies;
        treats = saved.treats;
        resting = saved.resting;
        carried = saved.carried;
    }

    boolean carry(Shiny item) {
        if (item != null && item.copies(shinies) == 0) return false;
        carried = item;
        return true;
    }

    void perch(float x, float y) {
        perchX = safeFraction(x, .47f);
        perchY = safeFraction(y, .78f);
    }

    static float safeFraction(float value, float fallback) {
        return Float.isNaN(value) || Float.isInfinite(value) ? fallback : Math.max(0, Math.min(1, value));
    }

    private static int clamp(int value, int min, int max) {
        return Math.max(min, Math.min(max, value));
    }
}
