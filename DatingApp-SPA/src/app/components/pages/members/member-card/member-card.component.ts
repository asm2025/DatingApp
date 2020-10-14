import { Component, Input, Output, EventEmitter } from "@angular/core";
import { of } from "rxjs";
import { catchError } from "rxjs/operators";

import { IUserForList, IUser } from "@data/model/User";
import UserClient from "@services/web/UserClient";
import AlertService from "@services/alert.service";

import config from "@/config.json";

@Component({
	selector: "app-member-card",
	templateUrl: "./member-card.component.html",
	styleUrls: ["./member-card.component.scss"]
})
export default class MemberCardComponent {
	@Input() user: IUserForList;
	@Output() liked = new EventEmitter<number>();
	@Output() disliked = new EventEmitter<number>();

	constructor(private readonly _userClient: UserClient,
		private readonly _alertService: AlertService) {
	}

	getUserImage(user: IUserForList): string {
		return user.photoUrl || config.users.defaultImage;
	}

	like(id: string) {
		const user = this._userClient.user as IUser;
		if (!user) return;
		this._userClient.like(user.id, id)
			.pipe(catchError(error => {
				this._alertService.toasts.error(error.toString());
				return of(-1);
			}))
			.subscribe((response: number) => {
				if (response < 0) return;
				this.liked.emit(response);
			});
	}

	dislike(id: string) {
		const user = this._userClient.user as IUser;
		if (!user) return;
		this._userClient.dislike(user.id, id)
			.pipe(catchError(error => {
				this._alertService.toasts.error(error.toString());
				return of(-1);
			}))
			.subscribe((response: number) => {
				if (response < 0) return;
				this.disliked.emit(response);
			});
	}
}