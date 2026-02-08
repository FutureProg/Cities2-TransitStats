import { ModRegistrar } from "cs2/modding";
import { TransitTransferFoldout } from "mods/TransitTransferFoldout";

const register: ModRegistrar = (moduleRegistry) => {


    // game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx -- shows Route statistics (length, stops, usage)    
    moduleRegistry.append(
        'game-ui/game/components/selected-info-panel/selected-info-panel.tsx',
        'Scrollable',
        TransitTransferFoldout,
        2
    );
}

export default register;