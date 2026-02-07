import { ModRegistrar } from "cs2/modding";
import { TransitTransferSankey } from "mods/TransitTransferSankey";

const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Menu', TransitTransferSankey);
}

export default register;