import { Injectable } from "@angular/core";
import { Resolve, Router, ActivatedRouteSnapshot } from "@angular/router";
import { Observable, of } from "rxjs";
import { catchError } from "rxjs/operators";

import { IPaginated } from "@common/pagination/Paginated";
import { IUserForList, IUserList } from "@data/model/User";
import UserClient from "@services/web/UserClient";
import AlertifyService from "@services/alertify.service";

@Injectable()
export default class ListsResolver implements Resolve<IPaginated<IUserForList>> {
	userList = {
		page: 1,
		pageSize: 10,
		minAge: 16,
		maxAge: 99
	} as IUserList;

	constructor(private readonly _userService: UserClient,
		private readonly _router: Router,
		private readonly _alertify: AlertifyService) {
	}

	resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<IPaginated<IUserForList>> | Promise<IPaginated<IUserForList>> | IPaginated<IUserForList> {
		return this._userService
			.list(this.userList)
			.pipe(
				catchError(error => {
					console.log(error);
					this._router.navigate(["/"]);
					return of(null);
				}));
	}
}