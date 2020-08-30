// https://github.com/angular/angular/blob/master/packages/forms/src/directives/ng_control_status.ts
import { Directive, Self } from "@angular/core";
import {
	AbstractControlDirective,
	ControlContainer,
	NgControl
} from "@angular/forms";

export class AbstractControlStatus {
	private _cd: AbstractControlDirective;

	constructor(cd: AbstractControlDirective) {
		this._cd = cd;
	}

	get ngClassUntouched(): boolean {
		return this._cd.control ? this._cd.control.untouched : false;
	}
	get ngClassTouched(): boolean {
		return this._cd.control ? this._cd.control.touched : false;
	}
	get ngClassPristine(): boolean {
		return this._cd.control ? this._cd.control.pristine : false;
	}
	get ngClassDirty(): boolean {
		return this._cd.control ? this._cd.control.dirty : false;
	}
	get ngClassValid(): boolean {
		return this._cd.control ? this._cd.control.valid : false;
	}
	get ngClassInvalid(): boolean {
		return this._cd.control ? this._cd.control.invalid : false;
	}
	get ngClassPending(): boolean {
		return this._cd.control ? this._cd.control.pending : false;
	}
}

export const ngControlStatusHost = {
	'[class.ng-untouched]': "ngClassUntouched",
	'[class.ng-touched]': "ngClassTouched",
	'[class.ng-pristine]': "ngClassPristine",
	'[class.ng-dirty]': "ngClassDirty",
	'[class.is-valid]': "ngClassValid",
	'[class.is-invalid]': "ngClassInvalid",
	'[class.ng-pending]': "ngClassPending",
};

/**
 * @description
 * Directive automatically applied to Angular form controls that sets CSS classes
 * based on control status.
 *
 * @usageNotes
 *
 * ### CSS classes applied
 *
 * The following classes are applied as the properties become true:
 *
 * * is-valid
 * * is-invalid
 * * ng-pending
 * * ng-pristine
 * * ng-dirty
 * * ng-untouched
 * * ng-touched
 *
 * @ngModule ReactiveFormsModule
 * @ngModule FormsModule
 * @publicApi
 */
@Directive({ selector: "[formControlName],[ngModel],[formControl],[ng-select]", host: ngControlStatusHost })
export class NgControlStatus extends AbstractControlStatus {
	constructor(@Self() cd: NgControl) {
		super(cd);
	}
}

/**
 * @description
 * Directive automatically applied to Angular form groups that sets CSS classes
 * based on control status (valid/invalid/dirty/etc).
 *
 * @see `NgControlStatus`
 *
 * @ngModule ReactiveFormsModule
 * @ngModule FormsModule
 * @publicApi
 */
@Directive({
	selector:
		"[formGroupName],[formArrayName],[ngModelGroup],[formGroup],form:not([ngNoForm]),[ngForm]",
	host: ngControlStatusHost
})
export class NgControlStatusGroup extends AbstractControlStatus {
	constructor(@Self() cd: ControlContainer) {
		super(cd);
	}
}
