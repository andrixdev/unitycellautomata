
using System.Runtime.InteropServices;

class Cell
{
	public static int lifeRange = 50;
	private byte status;
	private int xNbr, yNbr, zNbr;
	private int lifespan;
	private double mortality;
	private double natality;
	public readonly double cellLength;

	//Accesseurs et mutateurs
	public Cell(int xNbr, int yNbr, int zNbr, double cellLength, byte status, double mortality, double natality)
	{
		this.xNbr = xNbr;
		this.yNbr = yNbr;
		this.zNbr = zNbr;
		this.status = status;
		this.lifespan = 0;
		this.mortality = mortality;
		this.natality = natality;
		this.cellLength = cellLength;
		//this.transform.position = Vector3.right*(xNbr*cellLength + cellLength/2f) + Vector3.up*(yNbr*cellLength + cellLength/2) + Vector3.forward*(zNbr*cellLength + cellLength/2);
	}

	public byte getStatus()
	{
		return status;
	}

	public void changeStatus(byte new_status)
	{
		byte past_status = this.status;
		this.status = new_status;

		if (new_status > 0 && UnityEngine.Random.value < this.mortality)
			this.status = 0;
		if (new_status == 0 && UnityEngine.Random.value < this.natality)
			this.status = 1;

		if(past_status == 0 && this.status > 0)
        {
			this.resetLifespan();
        }
		else if(past_status > 0 && this.status == 0)
        {
			this.resetLifespan();
        }
		else if(past_status > 0 && this.status > 0)
        {
			this.incrLifespan();
        }
	}

	public int getLifespan()
	{
		return lifespan;
	}

	public void incrLifespan()
    {
		this.lifespan++;
		if (this.lifespan > Cell.lifeRange)
			this.lifespan = Cell.lifeRange;
    }

	public void resetLifespan()
	{
		this.lifespan = 0;
	}
	
	public Cell Clone()
	{
		return new Cell(this.xNbr,this.yNbr,this.zNbr,this.cellLength,this.status,this.mortality,this.natality);
	}

}
