using System.Runtime.InteropServices;
using UnityEngine;

class Cell
{
	private byte status;
	private int xNbr, yNbr, zNbr;
	private int birthdate;

	//Accesseurs et mutateurs
	public Cell(int xNbr, int yNbr, int zNbr, byte status)
	{
		this.xNbr = xNbr;
		this.yNbr = yNbr;
		this.zNbr = zNbr;
		this.status = status;
		this.birthdate = -1; // -1 means not born yet
	}

	public byte getStatus()
	{
		return status;
	}

	public void changeStatus(byte new_status)
	{
		byte past_status = this.status;
		this.status = new_status;

		if(past_status == 0 && this.status > 0)
        {
			this.resetBirthdate();
        }
		else if(past_status > 0 && this.status == 0)
        {
			this.resetBirthdate();
        }
		else if(past_status > 0 && this.status > 0)
        {
			// Do nada (leave frameTime as it is)
        }
	}

	public int getBirthdate()
	{
		return birthdate;
	}


	public void resetBirthdate()
	{
		this.birthdate = Time.frameCount;
	}
	
	public Cell Clone()
	{
		return new Cell(this.xNbr, this.yNbr, this.zNbr, this.status);
	}

}
