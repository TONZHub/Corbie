package com.tonzhub.corbie;

final class ShinyArt {
    static int icon(Shiny item) {
        switch (item) {
            case BOTTLE_CAP: return R.drawable.shiny_bottle_cap;
            case PAPERCLIP: return R.drawable.shiny_paperclip;
            case SEA_GLASS: return R.drawable.shiny_sea_glass;
            case ESC_KEY: return R.drawable.shiny_esc_key;
            case BRASS_NUT: return R.drawable.shiny_brass_nut;
            case PURPLE_D20: return R.drawable.shiny_purple_d20;
        }
        throw new AssertionError(item);
    }
}
