import { APP_INITIALIZER } from "@angular/core";

import UserClient from "@services/web/UserClient";

export function appInitializer(userClient: UserClient) {
	return () => new Promise(resolve => {
		// attempt to refresh token on app start up to auto authenticate.
		userClient.refreshToken().subscribe().add(resolve);
	});
}

const AppInitializerProvider = {
	provide: APP_INITIALIZER,
	useFactory: appInitializer,
	multi: true,
	deps: [UserClient]
};

export default AppInitializerProvider;
